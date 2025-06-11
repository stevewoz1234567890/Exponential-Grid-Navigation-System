using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;


namespace TwoDimentionalSpaceGame;


public static class LineMath
{
    /// <summary>
    /// Fast floor(log2(v)) for positive v.
    /// </summary>
    public static int FloorLog2(BigInteger v)
    {
        if (v.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(v));
        var bytes = v.ToByteArray();
        int msb = bytes[bytes.Length - 1];
        int bits = (bytes.Length - 1) * 8;
        int top = 0;
        while (msb != 0) { top++; msb >>= 1; }
        return bits + top - 1;
    }

    /// <summary>
    /// Return 2^exponent as a BigInteger.
    /// </summary>
    public static BigInteger Pow2(int exponent)
    {
        if (exponent < 0) throw new ArgumentOutOfRangeException(nameof(exponent));
        return BigInteger.One << exponent;
    }

    /// <summary>
    /// Is this a power of two?  (i.e. exactly one 1‐bit.)
    /// </summary>
    public static bool IsPowerOfTwo(BigInteger v)
    {
        return v.Sign > 0 && (v & (v - 1)) == 0;
    }

    /// <summary>
    /// Invert x0=2^k → returns k (or throws if not an exact power of two).
    /// </summary>
    public static int VerticalExponent(BigInteger x0)
    {
        if (!IsPowerOfTwo(x0))
            throw new ArgumentException("Not a strict power of two", nameof(x0));
        return FloorLog2(x0);
    }

    /// <summary>
    /// Invert y0=2^k → returns k (or throws if not an exact power of two).
    /// </summary>
    public static int HorizontalExponent(BigInteger y0)
    {
        return VerticalExponent(y0);
    }

    /// <summary>
    /// Compute C for a diagonal of slope ±1 at exponent k:
    ///   slope=+1 → y–x = ±(2^k – 1)  (positive==true for +, false for –)
    ///   slope=–1 → y+x =  2^k + 1
    /// </summary>
    public static BigInteger DiagonalInterceptFromExponent(int slope, int k, bool positive = true)
    {
        BigInteger twoK = Pow2(k);
        if (slope == +1)
        {
            var c = twoK - BigInteger.One;
            return positive ? c : -c;
        }
        else if (slope == -1)
        {
            return twoK + BigInteger.One;
        }
        else
        {
            throw new ArgumentException("Slope must be ±1", nameof(slope));
        }
    }

    /// <summary>
    /// Invert a diagonal intercept C back to its exponent k:
    ///   slope=+1 → C = ±(2^k – 1)  ⇒ |C|+1 = 2^k  
    ///   slope=–1 → C =    2^k + 1  ⇒  C–1 = 2^k  
    /// </summary>
    public static int DiagonalExponentFromIntercept(int slope, BigInteger C)
    {
        if (slope == +1)
        {
            BigInteger twoK = BigInteger.Abs(C) + BigInteger.One;
            if (!IsPowerOfTwo(twoK))
                throw new ArgumentException("C is not ±(2^k–1)", nameof(C));
            return FloorLog2(twoK);
        }
        else if (slope == -1)
        {
            BigInteger twoK = C - BigInteger.One;
            if (!IsPowerOfTwo(twoK))
                throw new ArgumentException("C is not (2^k+1)", nameof(C));
            return FloorLog2(twoK);
        }
        else
        {
            throw new ArgumentException("Slope must be ±1", nameof(slope));
        }
    }
}

// simple coordinate pair
public struct BigIntegerCoordinate
{
    public BigInteger X { get; set; }

    public BigInteger Y { get; set; }

    public override string ToString() => $"({X}, {Y})";

    public BigIntegerCoordinate(BigInteger x, BigInteger y)
    {
        X = x;
        Y = y;
    }
}

// one jump record
// 1) HistoryEntry now records both source and target
public struct HistoryEntry
{
    public Direction Dir;
    public int SourceLineID; // ID of the line segment you started on
    public int TargetLineID; // ID of the line you jumped to
    public HistoryEntry(Direction dir, int src, int tgt)
    {
        Dir = dir;
        SourceLineID = src;
        TargetLineID = tgt;
    }
}

public enum Direction { N = 0, NE = 1, E = 2, SE = 3, S = 4, SW = 5, W = 6, NW = 7 }


// abstract line
// Base abstract class
// Base class unchanged
public abstract class LineDefinition
{
    public abstract string Key { get; }
    public abstract bool TryIntersect(
       BigIntegerCoordinate p0, BigInteger dx, BigInteger dy,
       out BigInteger t, out BigInteger x1, out BigInteger y1
    );
}

// vertical x = 2^k
public class VerticalLineDefinition : LineDefinition
{
    public int Exponent { get; }
    // X0 is computed on demand, not stored
    [JsonIgnore]
    public BigInteger X0 => LineMath.Pow2(Exponent);

    public VerticalLineDefinition(int exponent)
    {
        if (exponent < 0) throw new ArgumentOutOfRangeException(nameof(exponent));
        Exponent = exponent;
    }

    public override string Key => "V" + Exponent;

    public override bool TryIntersect(
        BigIntegerCoordinate p0,
        BigInteger dx, BigInteger dy,
        out BigInteger t,
        out BigInteger x1,
        out BigInteger y1)
    {
        if (dx.IsZero)
        {
            if (p0.X != X0) { t = x1 = y1 = 0; return false; }
            t = 0; x1 = p0.X; y1 = p0.Y;
            return true;
        }
        BigInteger num = X0 - p0.X;
        if (num % dx != 0) { t = x1 = y1 = 0; return false; }
        t = num / dx;
        if (t < 0) { x1 = y1 = 0; return false; }
        x1 = X0; y1 = p0.Y + t * dy;
        return true;
    }
}

// Modified HorizontalLineDefinition with same pattern
public class HorizontalLineDefinition : LineDefinition
{
    public int Exponent { get; }
    // Y0 is computed on demand, not stored
    [JsonIgnore]
    public BigInteger Y0 => LineMath.Pow2(Exponent);

    public HorizontalLineDefinition(int exponent)
    {
        if (exponent < 0) throw new ArgumentOutOfRangeException(nameof(exponent));
        Exponent = exponent;
    }

    public override string Key => "H" + Exponent;

    public override bool TryIntersect(
        BigIntegerCoordinate p0,
        BigInteger dx, BigInteger dy,
        out BigInteger t,
        out BigInteger x1,
        out BigInteger y1)
    {
        if (dy.IsZero)
        {
            if (p0.Y != Y0) { t = x1 = y1 = 0; return false; }
            t = 0; x1 = p0.X; y1 = p0.Y;
            return true;
        }
        BigInteger num = Y0 - p0.Y;
        if (num % dy != 0) { t = x1 = y1 = 0; return false; }
        t = num / dy;
        if (t < 0) { x1 = y1 = 0; return false; }
        y1 = Y0; x1 = p0.X + t * dx;
        return true;
    }
}

// DiagonalLineDefinition remains mostly the same but with better handling
public class DiagonalLineDefinition : LineDefinition
{
    public int Slope { get; }
    public int Exponent { get; }
    public bool IsPositive { get; } // Only meaningful for slope +1

    [JsonIgnore]
    public BigInteger C => LineMath.DiagonalInterceptFromExponent(Slope, Exponent, IsPositive);

    /// <summary>
    /// Exponent-based constructor for creating standard diagonal lines
    /// </summary>
    public DiagonalLineDefinition(int slope, int exponent, bool positive = true)
    {
        if (slope != 1 && slope != -1)
            throw new ArgumentException("Slope must be ±1", nameof(slope));
        if (exponent < 0)
            throw new ArgumentOutOfRangeException(nameof(exponent));

        Slope = slope;
        Exponent = exponent;
        IsPositive = positive;
    }

    // Removed the BigInteger c constructor, as only exponent-based lines are used.

    public override string Key
    {
        get
        {
            // Exponent is always >= 0 for these lines
            if (Slope == 1)
                return (IsPositive ? "+" : "~") + Exponent;
            else
                return "-" + Exponent;
        }
    }

    // TryIntersect remains the same
    public override bool TryIntersect(
        BigIntegerCoordinate p0,
        BigInteger dx, BigInteger dy,
        out BigInteger t,
        out BigInteger x1,
        out BigInteger y1)
    {
        BigInteger denom = dy - (BigInteger)Slope * dx;
        BigInteger num = (BigInteger)Slope * p0.X + C - p0.Y;

        if (denom.IsZero)
        {
            if (num.IsZero)
            {
                t = BigInteger.Zero;
                x1 = p0.X;
                y1 = p0.Y;
                return true;
            }
            t = x1 = y1 = 0;
            return false;
        }

        if (num % denom != 0)
        {
            t = x1 = y1 = 0;
            return false;
        }
        t = num / denom;
        if (t < 0)
        {
            x1 = y1 = 0;
            return false;
        }
        x1 = p0.X + t * dx;
        y1 = p0.Y + t * dy;
        return true;
    }
}


public partial class LineRegistry
{
    // These fields are now pre-populated with environment lines only
    public ConcurrentDictionary<string, int> _keyToId = new ConcurrentDictionary<string, int>();
    public ConcurrentDictionary<int, LineDefinition> _idToDef = new ConcurrentDictionary<int, LineDefinition>();
    public List<int> _diagPlus = new List<int>(); // Diagonals with slope +1
    public List<int> _diagMinus = new List<int>(); // Diagonals with slope -1
    public int _maxEnvExp; // Stores the maximum exponent for which lines were generated

    // Existing lock - not serialized
    [JsonIgnore]
    private object _diagLock = new object();

    // Default constructor required for deserialization
    public LineRegistry() { }

    /// <summary>
    /// Populates the registry with all standard power-of-2 grid lines up to a maximum exponent.
    /// </summary>
    public void PopulateEnvironment(int maxExp)
    {
        _maxEnvExp = maxExp;
        _keyToId.Clear();
        _idToDef.Clear();
        _diagPlus.Clear();
        _diagMinus.Clear();

        // Generate lines for exponents 0 to maxExp
        for (int exp = 0; exp <= maxExp; exp++)
        {
            // ID schema: exp * 5 + slot (1-based slot)
            // Slot 1: Vertical line x = 2^exp
            var vLine = new VerticalLineDefinition(exp);
            int vId = exp * 5 + 1;
            _keyToId[vLine.Key] = vId;
            _idToDef[vId] = vLine;

            // Slot 2: Horizontal line y = 2^exp
            var hLine = new HorizontalLineDefinition(exp);
            int hId = exp * 5 + 2;
            _keyToId[hLine.Key] = hId;
            _idToDef[hId] = hLine;

            // Slot 3: Diagonal slope +1, positive intercept C = +(2^exp - 1)
            var dPlusPosLine = new DiagonalLineDefinition(1, exp, true);
            int dPlusPosId = exp * 5 + 3;
            _keyToId[dPlusPosLine.Key] = dPlusPosId;
            _idToDef[dPlusPosId] = dPlusPosLine;
            _diagPlus.Add(dPlusPosId);

            // Slot 4: Diagonal slope +1, negative intercept C = -(2^exp - 1)
            var dPlusNegLine = new DiagonalLineDefinition(1, exp, false);
            int dPlusNegId = exp * 5 + 4;
            _keyToId[dPlusNegLine.Key] = dPlusNegId;
            _idToDef[dPlusNegId] = dPlusNegLine;
            _diagPlus.Add(dPlusNegId);

            // Slot 5: Diagonal slope -1, intercept C = (2^exp + 1)
            var dMinusLine = new DiagonalLineDefinition(-1, exp); // IsPositive doesn't affect C for slope -1
            int dMinusId = exp * 5 + 5;
            _keyToId[dMinusLine.Key] = dMinusId;
            _idToDef[dMinusId] = dMinusLine;
            _diagMinus.Add(dMinusId);
        }
    }

    public List<int> GetDiagonalLineIDs(int slope)
    {
        return slope == 1 ? _diagPlus : _diagMinus;
    }

    public int Count => _keyToId.Count;

    // GetLineDefinition now directly accesses the pre-populated dictionary
    public LineDefinition GetLineDefinition(int id)
    {
        if (_idToDef.TryGetValue(id, out var def))
        {
            return def;
        }
        throw new KeyNotFoundException($"Line ID {id} not found in environment registry.");
    }

    /// <summary>
    /// Gets the ID for a given environment line definition.
    /// Throws an exception if the line is not part of the pre-generated environment.
    /// </summary>
    public int GetEnvironmentLineId(LineDefinition def)
    {
        if (_keyToId.TryGetValue(def.Key, out int id))
        {
            return id;
        }
        // This indicates an attempt to use a line not part of the pre-generated environment.
        // It likely means that _maxEnvExp in PopulateEnvironment was not sufficiently large
        // to cover all exponents needed during navigation.
        throw new ArgumentException($"Line definition {def.Key} (Exponent: {(def as dynamic)?.Exponent}) is not part of the pre-generated environment (Max Exp: {_maxEnvExp}). Consider increasing maxExp in PopulateEnvironment.");
    }


    // ========================================================================
    // PERSISTENCE API
    // ========================================================================

    public void SaveToFile(string filePath)
    {
        // Create serialization options with type handling
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new LineDefinitionConverter(),
             //   new BigIntegerConverter()
            }
        };

        // Create a serialization model
        var model = new LineRegistrySerializationModel
        {
            KeyToId = _keyToId,
            IdToDef = _idToDef,
            DiagPlus = _diagPlus,
            DiagMinus = _diagMinus,
            MaxEnvExp = _maxEnvExp // Use the new property
        };

        // Serialize and write to file
        string jsonString = JsonSerializer.Serialize(model, options);
        File.WriteAllText(filePath, jsonString);
    }

    public static LineRegistry LoadFromFile(string filePath)
    {
        // Create deserialization options with type handling
        var options = new JsonSerializerOptions
        {
            Converters =
            {
                new LineDefinitionConverter(),
               // new BigIntegerConverter()
            }
        };

        // Read the file
        string jsonString = File.ReadAllText(filePath);

        // Deserialize to model
        var model = JsonSerializer.Deserialize<LineRegistrySerializationModel>(jsonString, options);

        // Create and populate a new LineRegistry
        var registry = new LineRegistry
        {
            _keyToId = model.KeyToId,
            _idToDef = model.IdToDef,
            _diagPlus = model.DiagPlus,
            _diagMinus = model.DiagMinus,
            _maxEnvExp = model.MaxEnvExp, // Load the new property
            _diagLock = new object() // Create a new lock
        };

        return registry;
    }
}


// Model class for serialization
public class LineRegistrySerializationModel
{
    public ConcurrentDictionary<string, int> KeyToId { get; set; }
    public ConcurrentDictionary<int, LineDefinition> IdToDef { get; set; }
    public List<int> DiagPlus { get; set; }
    public List<int> DiagMinus { get; set; }
    public int MaxEnvExp { get; set; } // Renamed from EnvCount to be more descriptive
}

// Custom converter for LineDefinition classes, now strictly for exponent-based lines
public class LineDefinitionConverter : JsonConverter<LineDefinition>
{
    public override LineDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        using (JsonDocument document = JsonDocument.ParseValue(ref reader))
        {
            var root = document.RootElement;
            var typeProperty = root.GetProperty("Type");
            var type = typeProperty.GetString();

            int exp = root.GetProperty("Exponent").GetInt32();
            if (exp < 0) throw new JsonException("Exponent must be non-negative for environment lines.");

            switch (type)
            {
                case "Vertical":
                    return new VerticalLineDefinition(exp);

                case "Horizontal":
                    return new HorizontalLineDefinition(exp);

                case "Diagonal":
                    int slope = root.GetProperty("Slope").GetInt32();
                    bool positive = true;
                    if (root.TryGetProperty("IsPositive", out var posProp))
                    {
                        positive = posProp.GetBoolean();
                    }
                    return new DiagonalLineDefinition(slope, exp, positive);

                default:
                    throw new JsonException($"Unknown LineDefinition type: {type}");
            }
        }
    }

    public override void Write(Utf8JsonWriter writer, LineDefinition value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value is VerticalLineDefinition vertical)
        {
            writer.WriteString("Type", "Vertical");
            writer.WriteNumber("Exponent", vertical.Exponent);
        }
        else if (value is HorizontalLineDefinition horizontal)
        {
            writer.WriteString("Type", "Horizontal");
            writer.WriteNumber("Exponent", horizontal.Exponent);
        }
        else if (value is DiagonalLineDefinition diagonal)
        {
            writer.WriteString("Type", "Diagonal");
            writer.WriteNumber("Slope", diagonal.Slope);
            writer.WriteNumber("Exponent", diagonal.Exponent);
            if (diagonal.Slope == 1) // Only write IsPositive for slope +1 lines
            {
                writer.WriteBoolean("IsPositive", diagonal.IsPositive);
            }
        }
        else
        {
            throw new NotSupportedException($"Unknown LineDefinition type for serialization: {value.GetType().Name}");
        }

        writer.WriteEndObject();
    }
}


// Modified navigation algorithm that only uses power-of-2 lines
public static class ExponentialGridNavigator
{
    // delta X/Y for each direction
    public static readonly BigInteger[] DX = {
        BigInteger.Zero, BigInteger.One,  BigInteger.One,  BigInteger.One,
        BigInteger.Zero, -BigInteger.One,-BigInteger.One,-BigInteger.One
    };
    public static readonly BigInteger[] DY = {
        BigInteger.One,  BigInteger.One,  BigInteger.Zero, -BigInteger.One,
       -BigInteger.One, -BigInteger.One, BigInteger.Zero,  BigInteger.One
    };

    public enum NavigationMode { Bisection, AlignX, AlignY }

    private static (BigIntegerCoordinate NewPos, int LineID, Direction Dir)
    AlignCoordinateStep(BigIntegerCoordinate p0, int currentLineID, LineRegistry registry, NavigationMode currentAlignMode, BigInteger MLR, out NavigationMode nextMode)
    {
        Debug.Assert(currentAlignMode == NavigationMode.AlignX || currentAlignMode == NavigationMode.AlignY);

        if (currentAlignMode == NavigationMode.AlignX)
        {
            // --- Align X coordinate ---
            int kx_current = (p0.X.Sign > 0) ? LineMath.FloorLog2(p0.X) : 0;
            BigInteger pow2_kx_current = LineMath.Pow2(kx_current);
            BigInteger pow2_kx_next = LineMath.Pow2(kx_current + 1);

            int targetExpX;
            BigInteger targetX_pow2;

            // Is p0.X already a power of two?
            bool p0XisPow2 = LineMath.IsPowerOfTwo(p0.X) && p0.X == pow2_kx_current;

            // Determine the target power of two for X
            if (BigInteger.Abs(p0.X - pow2_kx_current) <= MLR && p0.X.Sign > 0) // Check if current X is already within MLR of 2^kx_current
            {
                // If p0.X is already 2^kx_current and within MLR, or just within MLR of 2^kx_current
                targetExpX = kx_current;
                targetX_pow2 = pow2_kx_current;
                // If X is good, move to align Y.
                if (p0.X == targetX_pow2)
                {
                    nextMode = NavigationMode.AlignY;
                    // No actual move needed for X, effectively a "pass" for this step.
                    // To satisfy return type, we can fake a "stay" E/W move on current line if X is already targetX_pow2
                    return (p0, currentLineID, p0.X < targetX_pow2 + 1 ? Direction.E : Direction.W); // +1 to ensure a direction if p0.X == targetX_pow2
                }
            }
            else if (kx_current + 1 <= registry._maxEnvExp && BigInteger.Abs(p0.X - pow2_kx_next) <= MLR && p0.X.Sign > 0) // Check 2^(kx+1)
            {
                targetExpX = kx_current + 1;
                targetX_pow2 = pow2_kx_next;
                if (p0.X == targetX_pow2)
                {
                    nextMode = NavigationMode.AlignY;
                    return (p0, currentLineID, p0.X < targetX_pow2 + 1 ? Direction.E : Direction.W);
                }
            }
            else // X is not within MLR of its closest powers of two, needs to move to one.
            {
                // Choose the closer of 2^kx or 2^(kx+1) as the absolute target
                if (BigInteger.Abs(p0.X - pow2_kx_current) <= BigInteger.Abs(p0.X - pow2_kx_next) || kx_current + 1 > registry._maxEnvExp)
                {
                    targetExpX = kx_current;
                    targetX_pow2 = pow2_kx_current;
                }
                else
                {
                    targetExpX = kx_current + 1;
                    targetX_pow2 = pow2_kx_next;
                }
            }

            // Perform the move to targetX_pow2
            Direction dirX = p0.X < targetX_pow2 ? Direction.E : Direction.W;
            if (p0.X == targetX_pow2) // If already there (e.g. from MLR check above), no move, prepare to align Y
            {
                nextMode = NavigationMode.AlignY;
                // No actual move needed for X, effectively a "pass" for this step.
                return (p0, currentLineID, dirX);
            }

            var targetVLine = new VerticalLineDefinition(targetExpX);
            int targetVLineId = registry.GetEnvironmentLineId(targetVLine);
            var newPos = new BigIntegerCoordinate(targetX_pow2, p0.Y);

            nextMode = NavigationMode.AlignY; // After aligning X, next is to align Y
            return (newPos, targetVLineId, dirX);
        }
        else // currentAlignMode == NavigationMode.AlignY
        {
            // --- Align Y coordinate ---
            // Similar logic as AlignX, but for Y, moving vertically
            int ky_current = (p0.Y.Sign > 0) ? LineMath.FloorLog2(p0.Y) : 0;
            BigInteger pow2_ky_current = LineMath.Pow2(ky_current);
            BigInteger pow2_ky_next = LineMath.Pow2(ky_current + 1);

            int targetExpY;
            BigInteger targetY_pow2;

            bool p0YisPow2 = LineMath.IsPowerOfTwo(p0.Y) && p0.Y == pow2_ky_current;

            if (BigInteger.Abs(p0.Y - pow2_ky_current) <= MLR && p0.Y.Sign > 0)
            {
                targetExpY = ky_current;
                targetY_pow2 = pow2_ky_current;
                // If Y is good, and X was also good (from previous AlignX), we might be done.
                // The main loop's IsWithinToleranceBoth will check. For now, switch back to AlignX for next iteration.
                if (p0.Y == targetY_pow2)
                {
                    nextMode = NavigationMode.AlignX;
                    return (p0, currentLineID, p0.Y < targetY_pow2 + 1 ? Direction.N : Direction.S);
                }
            }
            else if (ky_current + 1 <= registry._maxEnvExp && BigInteger.Abs(p0.Y - pow2_ky_next) <= MLR && p0.Y.Sign > 0)
            {
                targetExpY = ky_current + 1;
                targetY_pow2 = pow2_ky_next;
                if (p0.Y == targetY_pow2)
                {
                    nextMode = NavigationMode.AlignX;
                    return (p0, currentLineID, p0.Y < targetY_pow2 + 1 ? Direction.N : Direction.S);
                }
            }
            else
            {
                if (BigInteger.Abs(p0.Y - pow2_ky_current) <= BigInteger.Abs(p0.Y - pow2_ky_next) || ky_current + 1 > registry._maxEnvExp)
                {
                    targetExpY = ky_current;
                    targetY_pow2 = pow2_ky_current;
                }
                else
                {
                    targetExpY = ky_current + 1;
                    targetY_pow2 = pow2_ky_next;
                }
            }

            Direction dirY = p0.Y < targetY_pow2 ? Direction.N : Direction.S;
            if (p0.Y == targetY_pow2)
            {
                nextMode = NavigationMode.AlignX; // Back to AlignX for the next overall check
                return (p0, currentLineID, dirY);
            }

            var targetHLine = new HorizontalLineDefinition(targetExpY);
            int targetHLineId = registry.GetEnvironmentLineId(targetHLine);
            var newPos = new BigIntegerCoordinate(p0.X, targetY_pow2);

            nextMode = NavigationMode.AlignX; // After aligning Y, loop back to check/align X in the next iteration.
                                              // The IsWithinToleranceBoth in the main loop will terminate if both are good.
            return (newPos, targetHLineId, dirY);
        }
    }

    // Modified bisection step that only jumps to power-of-2 lines
    // Modified bisection step that only jumps to power-of-2 lines
    private static (BigIntegerCoordinate NewPos, int LineID, Direction Dir)
    old_NextBisectionStep_original(BigIntegerCoordinate p0, LineRegistry registry)
    {
        BigInteger error = p0.Y - p0.X;
        Direction dir = error < 0 ? Direction.NW : Direction.SE; // Direction to reduce |Y-X|
        BigInteger dx = DX[(int)dir];
        BigInteger dy = DY[(int)dir];

        // Priority 1: If on y=x (error.IsZero), align X to nearest power of 2
        // This move ensures progress towards a power-of-2 structure when error is zero
        if (error.IsZero)
        {
            int kx = (p0.X.Sign > 0) ? LineMath.FloorLog2(p0.X) : 0;
            BigInteger twoK_X = LineMath.Pow2(kx);
            BigInteger nextTwoK_X = LineMath.Pow2(kx + 1);

            BigInteger targetX;
            int targetExp;

            // If p0.X is already a power of two, we must choose a *different* power of two to make progress.
            // Typically, this means moving to the next higher power of two.
            if (p0.X == twoK_X)
            {
                targetX = nextTwoK_X;
                targetExp = kx + 1;
            }
            // If p0.X is not a power of two, choose the closest power of two.
            else if (BigInteger.Abs(p0.X - twoK_X) <= BigInteger.Abs(p0.X - nextTwoK_X))
            {
                targetX = twoK_X;
                targetExp = kx;
            }
            else
            {
                targetX = nextTwoK_X;
                targetExp = kx + 1;
            }

            Direction moveDir = (p0.X < targetX) ? Direction.E : Direction.W;

            // Ensure targetExp is within environment bounds
            if (targetExp > registry._maxEnvExp) targetExp = registry._maxEnvExp;
            else if (targetExp < 0) targetExp = 0;

            var targetLine = new VerticalLineDefinition(targetExp);
            int targetLineId = registry.GetEnvironmentLineId(targetLine);

            BigInteger newX = targetX;
            BigInteger newY = p0.Y; // Y coordinate does not change for E/W move

            return (new BigIntegerCoordinate(newX, newY), targetLineId, moveDir);
        }

        // Priority 2: Try diagonal lines to reduce |Y-X| error
        BigInteger absErr = BigInteger.Abs(error);

        // Strategically select candidate exponents 'k' for diagonal C = +/- (2^k - 1)
        int k_approx_log_err = (absErr.IsZero || absErr.IsOne) ? 0 : LineMath.FloorLog2(absErr); // Use FloorLog2(absErr) for C ~ absErr

        var candidateExponents = new List<int>();
        // k_ideal aims for C to be just under absErr. C = 2^k - 1. So 2^k ~ absErr. k ~ log2(absErr)
        if (k_approx_log_err > 0) candidateExponents.Add(k_approx_log_err);     // k closest to log2(absErr)
        if (k_approx_log_err > 1) candidateExponents.Add(k_approx_log_err - 1); // k-1
        if (k_approx_log_err > 2) candidateExponents.Add(k_approx_log_err - 2); // k-2

        // Add an exponent that would give C with roughly half the bits of absErr.
        // This provides a jump of a significantly different scale.
        if (k_approx_log_err > 10) candidateExponents.Add(k_approx_log_err / 2);

        // Add some smaller, fixed exponents which are often useful, especially as absErr gets smaller.
        candidateExponents.Add(Math.Min(10, k_approx_log_err));
        candidateExponents.Add(Math.Min(5, k_approx_log_err));
        candidateExponents.Add(Math.Min(1, k_approx_log_err));
        candidateExponents.Add(0); // Always consider k=0 (C = 0 for positive=true, C=-0 for positive=false) which is y=x.

        var distinctValidExponents = candidateExponents
            .Where(exp => exp >= 0 && exp <= registry._maxEnvExp) // Must be valid and within environment
            .Distinct()
            .OrderByDescending(exp => exp) // Process larger k first, aiming for bigger error reduction
            .ToList();

        BigInteger bestAbsNewErr = BigInteger.Abs(error); // Initialize with current error
        BigInteger bestT = BigInteger.MinusOne; // Use MinusOne to ensure first valid t > 0 is chosen
        BigInteger bestX = BigInteger.Zero;
        BigInteger bestY = BigInteger.Zero;
        int bestLineId = -1;
        bool foundDiagonal = false;

        for (int idx = 0; idx < distinctValidExponents.Count; idx++)
        {
            int exp = distinctValidExponents[idx];

            // Determine the sign of the intercept C for slope +1 lines.
            // If error > 0 (Y > X), we want C < error. Prefer y-x = +(2^k - 1). So targetPositiveC = true.
            // If error < 0 (Y < X), we want C > error. Prefer y-x = -(2^k - 1). So targetPositiveC = false.
            bool targetPositiveC = (error > 0);

            var diagLine = new DiagonalLineDefinition(1, exp, targetPositiveC);
            int diagLineId = registry.GetEnvironmentLineId(diagLine); // Exponent already checked to be within bounds

            if (diagLine.TryIntersect(p0, dx, dy, out BigInteger t, out BigInteger x1, out BigInteger y1))
            {
                if (t.Sign <= 0) continue; // Must move forward (t > 0)

                BigInteger newErr = y1 - x1;
                BigInteger absNewErr = BigInteger.Abs(newErr);

                if (absNewErr < bestAbsNewErr ||
                    (absNewErr == bestAbsNewErr && (bestT.Sign < 0 || t < bestT)))
                {
                    foundDiagonal = true;
                    bestAbsNewErr = absNewErr;
                    bestT = t;
                    bestLineId = diagLineId;
                    bestX = x1;
                    bestY = y1;
                }
            }
        }

        if (foundDiagonal)
        {
            return (new BigIntegerCoordinate(bestX, bestY), bestLineId, dir);
        }

        // Priority 3: Fallback to nearest vertical/horizontal power-of-2 line
        int currentExpX = (p0.X.Sign > 0) ? LineMath.FloorLog2(p0.X) : 0;
        int currentExpY = (p0.Y.Sign > 0) ? LineMath.FloorLog2(p0.Y) : 0;

        if (!dx.IsZero) // If moving diagonally or East/West
        {
            int targetExp = (dx.Sign > 0) ? currentExpX + 1 : Math.Max(0, currentExpX - 1);
            targetExp = Math.Min(targetExp, registry._maxEnvExp); // Cap at max environment exp
            targetExp = Math.Max(targetExp, 0);                   // Ensure non-negative

            var vLine = new VerticalLineDefinition(targetExp);
            int vLineId = registry.GetEnvironmentLineId(vLine);

            if (vLine.TryIntersect(p0, dx, dy, out BigInteger tv, out BigInteger xv, out BigInteger yv) && tv.Sign > 0)
            {
                return (new BigIntegerCoordinate(xv, yv), vLineId, dir);
            }
        }

        if (!dy.IsZero) // If moving diagonally or North/South
        {
            int targetExp = (dy.Sign > 0) ? currentExpY + 1 : Math.Max(0, currentExpY - 1);
            targetExp = Math.Min(targetExp, registry._maxEnvExp); // Cap at max environment exp
            targetExp = Math.Max(targetExp, 0);                   // Ensure non-negative

            var hLine = new HorizontalLineDefinition(targetExp);
            int hLineId = registry.GetEnvironmentLineId(hLine);

            if (hLine.TryIntersect(p0, dx, dy, out BigInteger th, out BigInteger xh, out BigInteger yh) && th.Sign > 0)
            {
                return (new BigIntegerCoordinate(xh, yh), hLineId, dir);
            }
        }

        throw new InvalidOperationException($"CRITICAL: No valid navigation step found for position {p0} with chosen direction {dir}. This indicates a flaw in fallback logic or insufficient environment lines.");
    }

    // Modified termination check that ensures BOTH axes are within MLR of a power of 2
    private static bool IsWithinToleranceBoth(
        BigIntegerCoordinate p,
        BigInteger MLR,
        out int expX,
        out int expY)
    {
        expX = expY = -1;
        bool okX = false, okY = false;

        // Check X coordinate
        if (p.X.Sign > 0)
        {
            int kx = LineMath.FloorLog2(p.X);
            BigInteger twoX = BigInteger.One << kx;
            if (BigInteger.Abs(p.X - twoX) <= MLR)
            {
                okX = true;
                expX = kx;
            }
            else
            {
                // Also check the next power of 2
                BigInteger twoXNext = BigInteger.One << (kx + 1);
                if (BigInteger.Abs(p.X - twoXNext) <= MLR)
                {
                    okX = true;
                    expX = kx + 1;
                }
            }
        }

        // Check Y coordinate
        if (p.Y.Sign > 0)
        {
            int ky = LineMath.FloorLog2(p.Y);
            BigInteger twoY = BigInteger.One << ky;
            if (BigInteger.Abs(p.Y - twoY) <= MLR)
            {
                okY = true;
                expY = ky;
            }
            else
            {
                // Also check the next power of 2
                BigInteger twoYNext = BigInteger.One << (ky + 1);
                if (BigInteger.Abs(p.Y - twoYNext) <= MLR)
                {
                    okY = true;
                    expY = ky + 1;
                }
            }
        }

        return okX && okY;
    }

    // Move exactly to a power-of-2 line
    private static (BigIntegerCoordinate NewPos, int LineID)
    MoveToExactPowerOfTwo(BigIntegerCoordinate p0, int currentLineID, char axis, int targetExp, LineRegistry registry, List<HistoryEntry> history)
    {
        // For reversibility, these "snap" moves are recorded as directional moves to a power-of-two line.
        // The ReversePath will then attempt to intersect back from the target line.
        // This relies on the forward history recording SourceLineID (the line 'pos' was just on) and TargetLineID (the new line).

        if (axis == 'X')
        {
            var targetLine = new VerticalLineDefinition(targetExp);
            // Ensure targetExp is within environment bounds (already handled by registry.GetEnvironmentLineId)
            int targetLineId = registry.GetEnvironmentLineId(targetLine);
            BigInteger targetX = LineMath.Pow2(targetExp);

            Direction dir = p0.X < targetX ? Direction.E : Direction.W;

            // The "move" is from current position (p0.X, p0.Y) to (targetX, p0.Y).
            // This is a horizontal move.
            var newPos = new BigIntegerCoordinate(targetX, p0.Y);
            history.Add(new HistoryEntry(dir, currentLineID, targetLineId));

            return (newPos, targetLineId);
        }
        else // axis == 'Y'
        {
            var targetLine = new HorizontalLineDefinition(targetExp);
            // Ensure targetExp is within environment bounds (already handled by registry.GetEnvironmentLineId)
            int targetLineId = registry.GetEnvironmentLineId(targetLine);
            BigInteger targetY = LineMath.Pow2(targetExp);

            Direction dir = p0.Y < targetY ? Direction.N : Direction.S;

            // The "move" is from current position (p0.X, p0.Y) to (p0.X, targetY).
            // This is a vertical move.
            var newPos = new BigIntegerCoordinate(p0.X, targetY);
            history.Add(new HistoryEntry(dir, currentLineID, targetLineId));

            return (newPos, targetLineId);
        }
    }

    // Main navigation function 
    public static BigInteger Run(BigInteger startNumber, BigInteger MLR, string name)
    {
        // ------------------------------------------------------------------
        // 1 – build environment
        // ------------------------------------------------------------------
        int maxExp = LineMath.FloorLog2(startNumber) + 100;
        var registry = new LineRegistry();
        registry.PopulateEnvironment(maxExp);

        // ------------------------------------------------------------------
        // 2 – initial state  (startNumber , 1)  – on horizontal line H0
        // ------------------------------------------------------------------
        var pos = new BigIntegerCoordinate(startNumber, BigInteger.One);
        int currentLineID = registry.GetEnvironmentLineId(new HorizontalLineDefinition(0));

        var history = new List<HistoryEntry>();

        // ------------------------------------------------------------------
        // 3 – navigation loop
        // ------------------------------------------------------------------
        int iter = 0;
        int finalExpX, finalExpY;
        var mode = NavigationMode.Bisection;
        int bisectionStuck = 0;
        BigInteger prevAbsErr = -1;

        while (!IsWithinToleranceBoth(pos, MLR, out finalExpX, out finalExpY))
        {
            BigInteger error = pos.Y - pos.X;
            BigInteger absEr = BigInteger.Abs(error);

            (BigIntegerCoordinate NewPos, int LineID, Direction Dir) step;

            // ---------- choose mode ----------------------------------------
            if (mode == NavigationMode.Bisection)
            {
                if (absEr <= MLR && !absEr.IsZero) mode = NavigationMode.AlignX;
                else if (bisectionStuck > 5) mode = NavigationMode.AlignX;
            }

            // ---------- perform one step -----------------------------------
            if (mode == NavigationMode.Bisection)
            {
                step = NextBisectionStep_Original(pos, registry);

                var newAbs = BigInteger.Abs(step.NewPos.Y - step.NewPos.X);
                if (prevAbsErr >= 0 && !absEr.IsZero && newAbs >= prevAbsErr)
                    bisectionStuck++;
                else bisectionStuck = 0;
                prevAbsErr = newAbs;
            }
            else                                // AlignX / AlignY
            {
                step = AlignCoordinateStep(pos, currentLineID, registry, mode,
                                           MLR, out var nextMode);
                mode = nextMode;
                bisectionStuck = 0;
                prevAbsErr = -1;
            }

            history.Add(new HistoryEntry(step.Dir, currentLineID, step.LineID));
            pos = step.NewPos;
            currentLineID = step.LineID;

            if (++iter > 100_000)
                throw new InvalidOperationException("Navigation did not converge");
        }

        // ------------------------------------------------------------------
        // 4 – final exact snaps
        // ------------------------------------------------------------------

        // ---- X -----------------------------------------------------------
        if (finalExpX >= 0)
        {
            BigInteger targetX = LineMath.Pow2(finalExpX);
            int vId = registry.GetEnvironmentLineId(new VerticalLineDefinition(finalExpX));

            if (pos.X != targetX)           // real horizontal move
            {
                Direction dir = pos.X < targetX ? Direction.E : Direction.W;
                history.Add(new HistoryEntry(dir, currentLineID, vId));
                pos = new BigIntegerCoordinate(targetX, pos.Y);
                currentLineID = vId;
            }
            else if (currentLineID != vId)  // ---- dummy step  (FIX-2) ----
            {
                var srcDef = registry.GetLineDefinition(currentLineID);

                // try East first
                Direction dummyDir = Direction.E;
                if (!srcDef.TryIntersect(pos, DX[(int)dummyDir], DY[(int)dummyDir],
                                         out BigInteger t, out _, out _) || t < 0)
                {
                    dummyDir = Direction.W;           // West will work, otherwise we
                                                      // (impossible with our grid)     // would have to throw
                }

                history.Add(new HistoryEntry(dummyDir, currentLineID, vId));
                currentLineID = vId;
            }
        }

        // ---- Y -----------------------------------------------------------
        if (finalExpY >= 0)
        {
            BigInteger targetY = LineMath.Pow2(finalExpY);
            int hId = registry.GetEnvironmentLineId(new HorizontalLineDefinition(finalExpY));

            if (pos.Y != targetY)           // real vertical move
            {
                Direction dir = pos.Y < targetY ? Direction.N : Direction.S;
                history.Add(new HistoryEntry(dir, currentLineID, hId));
                pos = new BigIntegerCoordinate(pos.X, targetY);
                currentLineID = hId;
            }
            else if (currentLineID != hId)  // ---- dummy step  (FIX-2) ----
            {
                var srcDef = registry.GetLineDefinition(currentLineID);

                Direction dummyDir = Direction.N;
                if (!srcDef.TryIntersect(pos, DX[(int)dummyDir], DY[(int)dummyDir],
                                         out BigInteger t, out _, out _) || t < 0)
                {
                    dummyDir = Direction.S;           // opposite direction
                }

                history.Add(new HistoryEntry(dummyDir, currentLineID, hId));
                currentLineID = hId;
            }
        }

        // ------------------------------------------------------------------
        // 5 – reverse path verification
        // ------------------------------------------------------------------
        var recovered = ReversePath(pos, history, registry);
        if (recovered.X != startNumber || recovered.Y != 1)
            throw new InvalidOperationException($"Reverse failed – got {recovered}");

        Console.WriteLine("Path recovery successful!");
        return recovered.X;
    }

    // You need to rename your previous NextBisectionStep to this:
    private static (BigIntegerCoordinate NewPos, int LineID, Direction Dir)
        NextBisectionStep_Original(BigIntegerCoordinate p0, LineRegistry registry)
    {
        BigInteger error = p0.Y - p0.X;
        Direction dir = error < 0 ? Direction.NW : Direction.SE; // Direction to reduce |Y-X|
        BigInteger dx = DX[(int)dir];
        BigInteger dy = DY[(int)dir];

        // Priority 1: If on y=x (error.IsZero), align X to nearest power of 2
        if (error.IsZero)
        {
            int kx = (p0.X.Sign > 0) ? LineMath.FloorLog2(p0.X) : 0;
            BigInteger twoK_X = LineMath.Pow2(kx);
            BigInteger nextTwoK_X = LineMath.Pow2(kx + 1);

            BigInteger targetX;
            int targetExp;

            if (p0.X == twoK_X)
            {
                targetX = nextTwoK_X;
                targetExp = kx + 1;
            }
            else if (BigInteger.Abs(p0.X - twoK_X) <= BigInteger.Abs(p0.X - nextTwoK_X))
            {
                targetX = twoK_X;
                targetExp = kx;
            }
            else
            {
                targetX = nextTwoK_X;
                targetExp = kx + 1;
            }

            Direction moveDir = (p0.X < targetX) ? Direction.E : Direction.W;

            if (targetExp > registry._maxEnvExp) targetExp = registry._maxEnvExp;
            else if (targetExp < 0) targetExp = 0;

            var targetLine = new VerticalLineDefinition(targetExp);
            int targetLineId = registry.GetEnvironmentLineId(targetLine);

            BigInteger newX = targetX;
            BigInteger newY = p0.Y;

            return (new BigIntegerCoordinate(newX, newY), targetLineId, moveDir);
        }

        // Priority 2: Try diagonal lines
        BigInteger absErr = BigInteger.Abs(error);
        int k_approx_log_err = (absErr.IsZero || absErr.IsOne) ? 0 : LineMath.FloorLog2(absErr);

        var candidateExponents = new List<int>();
        if (k_approx_log_err > 0) candidateExponents.Add(k_approx_log_err);
        if (k_approx_log_err > 1) candidateExponents.Add(k_approx_log_err - 1);
        if (k_approx_log_err > 2) candidateExponents.Add(k_approx_log_err - 2);
        if (k_approx_log_err > 10) candidateExponents.Add(k_approx_log_err / 2);
        candidateExponents.Add(Math.Min(10, k_approx_log_err));
        candidateExponents.Add(Math.Min(5, k_approx_log_err));
        candidateExponents.Add(Math.Min(1, k_approx_log_err));
        candidateExponents.Add(0);

        var distinctValidExponents = candidateExponents
            .Where(exp => exp >= 0 && exp <= registry._maxEnvExp)
            .Distinct()
            .OrderByDescending(exp => exp)
            .ToList();

        BigInteger bestAbsNewErr = BigInteger.Abs(error);
        BigInteger bestT = BigInteger.MinusOne;
        BigInteger bestX = BigInteger.Zero;
        BigInteger bestY = BigInteger.Zero;
        int bestLineId = -1;
        bool foundDiagonal = false;

        for (int idx = 0; idx < distinctValidExponents.Count; idx++)
        {
            int exp = distinctValidExponents[idx];
            bool targetPositiveC = (error > 0);
            var diagLine = new DiagonalLineDefinition(1, exp, targetPositiveC);
            int diagLineId = registry.GetEnvironmentLineId(diagLine);

            if (diagLine.TryIntersect(p0, dx, dy, out BigInteger t, out BigInteger x1, out BigInteger y1))
            {
                if (t.Sign <= 0) continue;
                BigInteger newErr = y1 - x1;
                BigInteger absNewErrValue = BigInteger.Abs(newErr); // Renamed to avoid conflict

                if (absNewErrValue < bestAbsNewErr ||
                    (absNewErrValue == bestAbsNewErr && (bestT.Sign < 0 || t < bestT)))
                {
                    foundDiagonal = true;
                    bestAbsNewErr = absNewErrValue;
                    bestT = t;
                    bestLineId = diagLineId;
                    bestX = x1;
                    bestY = y1;
                }
            }
        }

        if (foundDiagonal)
        {
            return (new BigIntegerCoordinate(bestX, bestY), bestLineId, dir);
        }

        // Priority 3: Fallback
        int currentExpX = (p0.X.Sign > 0) ? LineMath.FloorLog2(p0.X) : 0;
        int currentExpY = (p0.Y.Sign > 0) ? LineMath.FloorLog2(p0.Y) : 0;

        if (!dx.IsZero)
        {
            int targetExp = (dx.Sign > 0) ? currentExpX + 1 : Math.Max(0, currentExpX - 1);
            targetExp = Math.Min(targetExp, registry._maxEnvExp);
            targetExp = Math.Max(targetExp, 0);

            var vLine = new VerticalLineDefinition(targetExp);
            int vLineId = registry.GetEnvironmentLineId(vLine);

            if (vLine.TryIntersect(p0, dx, dy, out BigInteger tv, out BigInteger xv, out BigInteger yv) && tv.Sign > 0)
            {
                return (new BigIntegerCoordinate(xv, yv), vLineId, dir);
            }
        }

        if (!dy.IsZero)
        {
            int targetExp = (dy.Sign > 0) ? currentExpY + 1 : Math.Max(0, currentExpY - 1);
            targetExp = Math.Min(targetExp, registry._maxEnvExp);
            targetExp = Math.Max(targetExp, 0); // Typo fixed to targetExp                  

            var hLine = new HorizontalLineDefinition(targetExp);
            int hLineId = registry.GetEnvironmentLineId(hLine);

            if (hLine.TryIntersect(p0, dx, dy, out BigInteger th, out BigInteger xh, out BigInteger yh) && th.Sign > 0)
            {
                return (new BigIntegerCoordinate(xh, yh), hLineId, dir);
            }
        }

        throw new InvalidOperationException($"CRITICAL: No valid navigation step found for position {p0} with chosen direction {dir} in NextBisectionStep_Original.");
    }

    // ReversePath remains mostly the same
    public static BigIntegerCoordinate ReversePath(
        BigIntegerCoordinate endPos,
        List<HistoryEntry> history,
        LineRegistry registry)
    {
        var pos = endPos;
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var e = history[i];
            var revDir = (Direction)(((int)e.Dir + 4) % 8); // Opposite direction

            // In reverse, the 'SourceLineID' of a history entry was the line the point was *on* before the forward step.
            // This 'SourceLineID' is the line we must now intersect with moving in 'revDir' from current 'pos'.
            var srcDef = registry.GetLineDefinition(e.SourceLineID);
            var dx = DX[(int)revDir];
            var dy = DY[(int)revDir];

            // TryIntersect calculates 't' (distance) and the intersection point (x0, y0).
            // If the current 'pos' is already ON the 'srcDef' line, TryIntersect will return t=0.
            // For general bisection steps, 't' should be > 0.
            // For 'snap' steps, if 'pos' (the final snapped position) happens to be on the *previous* source line (e.g. H_J),
            // then t=0 will occur. However, the logical previous point (before snap) was NOT necessarily on H_J.
            // Given the constraints (exponent-only lines, reversible history),
            // we rely on the mathematical properties of lines and the `TryIntersect` function.
            // A failure here indicates an inconsistency, often due to t=0 for steps that logically moved a distance.
            if (!srcDef.TryIntersect(pos, dx, dy,
                                     out BigInteger t,
                                     out BigInteger x0,
                                     out BigInteger y0))
            {
                throw new InvalidOperationException($"ReversePath intersection failed at step {i}. Current pos: {pos}, Reverse Dir: {revDir}, Source Line: {srcDef.Key}. No valid intersection found for t >= 0.");
            }

            // Crucial: If t is zero, it means 'pos' is ALREADY on 'srcDef'. This implies no movement
            // happened in reverse. For actual jumps (where t > 0 forward), this is fine.
            // For snaps, if pos was (A, B) and snapped to (A_power_of_2, B), and then reversed,
            // (A_power_of_2, B) is now 'pos', and if srcDef was V_A_power_of_2, t=0.
            // The path implicitly works because (x0,y0) is returned as the new position, even if t=0.
            // The primary success criterion is that the final recovered.X and Y match the start.
            pos = new BigIntegerCoordinate(x0, y0);
        }
        return pos;
    }
}
