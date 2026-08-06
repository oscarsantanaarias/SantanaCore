using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using SantanaLib.IO;
using ProudNetSrc;

namespace ProudNetSrc.Serialization
{

    public interface IMessage { }

    public enum PacketType { Auth, Game, GameRule, Chat, Club, Relay, P2P, Event }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PacketAttribute : Attribute
    {
        public ushort OpCode { get; }
        public PacketType Type { get; }
        public PacketAttribute(int opCode, PacketType type) { OpCode = (ushort)opCode; Type = type; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class DtoAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)] public sealed class CompressedAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)] public sealed class ScalarAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)] public sealed class IndexAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)] public sealed class IntBoolAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)] public sealed class EndpointStrAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)] public sealed class SecAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)] public sealed class SkipAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)] public sealed class FixedAttribute : Attribute { public int N; public FixedAttribute(int n) { N = n; } }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)] public sealed class WireAttribute : Attribute { public Kind Kind; public WireAttribute(Kind kind) { Kind = kind; } }

    public enum Kind
    {
        Bool, Byte, SByte, Short, UShort, Int, UInt, Long, ULong, Float, Double,
        Str, Guid, Version, Endpoint, EndpointString, RoomLocation, IntBoolean, UnixTime, TimeSpanMs, TimeSpanSeconds,
        CompressedFloat, CompressedVector, RotationVector,
        Color,
        FixedBytes, ArrayInt, ArrayScalar, ArrayIntIndex, FixedArray, Struct
    }

    public sealed class FieldType
    {
        public readonly Kind Kind;
        public readonly FieldType Elem;
        public readonly Type StructType;
        public readonly int FixedLen;

        public FieldType(Kind kind, FieldType elem = null, Type structType = null, int fixedLen = 0)
        {
            Kind = kind; Elem = elem; StructType = structType; FixedLen = fixedLen;
        }
    }

    public sealed class Fields : List<(string Name, FieldType Type)>
    {
        public void Add(string name, FieldType type) => Add((name, type));
    }

    public static class Packet
    {
        public static readonly FieldType Bool = new(Kind.Bool);
        public static readonly FieldType Byte = new(Kind.Byte);
        public static readonly FieldType SByte = new(Kind.SByte);
        public static readonly FieldType Short = new(Kind.Short);
        public static readonly FieldType UShort = new(Kind.UShort);
        public static readonly FieldType Int = new(Kind.Int);
        public static readonly FieldType UInt = new(Kind.UInt);
        public static readonly FieldType Long = new(Kind.Long);
        public static readonly FieldType ULong = new(Kind.ULong);
        public static readonly FieldType Float = new(Kind.Float);
        public static readonly FieldType Double = new(Kind.Double);
        public static readonly FieldType Str = new(Kind.Str);
        public static readonly FieldType Guid = new(Kind.Guid);
        public static readonly FieldType Version = new(Kind.Version);
        public static readonly FieldType Endpoint = new(Kind.Endpoint);
        public static readonly FieldType EndpointString = new(Kind.EndpointString);
        public static readonly FieldType RoomLocation = new(Kind.RoomLocation);
        public static readonly FieldType IntBoolean = new(Kind.IntBoolean);
        public static readonly FieldType UnixTime = new(Kind.UnixTime);
        public static readonly FieldType TimeSpanMs = new(Kind.TimeSpanMs);
        public static readonly FieldType TimeSpanSeconds = new(Kind.TimeSpanSeconds);
        public static readonly FieldType CompressedFloat = new(Kind.CompressedFloat);
        public static readonly FieldType CompressedVector = new(Kind.CompressedVector);
        public static readonly FieldType RotationVector = new(Kind.RotationVector);
        public static readonly FieldType ColorArgb = new(Kind.Color);

        public static FieldType ItemNumber => ULong;

        public static FieldType FixedBytes(int n) => new(Kind.FixedBytes, fixedLen: n);
        public static FieldType FixedArrayOf(int n, FieldType elem) => new(Kind.FixedArray, elem: elem, fixedLen: n);
        public static FieldType FixedArrayOf(int n, Type dto) => new(Kind.FixedArray, structType: dto, fixedLen: n);
        public static FieldType Struct(Type t) => new(Kind.Struct, structType: t);
        public static FieldType ArrayOf(FieldType elem) => new(Kind.ArrayInt, elem: elem);
        public static FieldType ArrayOf(Type dto) => new(Kind.ArrayInt, structType: dto);
        public static FieldType ScalarArrayOf(FieldType elem) => new(Kind.ArrayScalar, elem: elem);
        public static FieldType ScalarArrayOf(Type dto) => new(Kind.ArrayScalar, structType: dto);
        public static FieldType IndexArrayOf(FieldType elem) => new(Kind.ArrayIntIndex, elem: elem);
        public static FieldType IndexArrayOf(Type dto) => new(Kind.ArrayIntIndex, structType: dto);

        private sealed class Schema { public List<(string Name, FieldType Type)> Fields; }
        private static readonly Dictionary<Type, Schema> _schemas = new();
        private static readonly Dictionary<Type, ushort> _opByType = new();
        private static readonly Dictionary<string, Dictionary<ushort, Type>> _byMethod = new();
        private static readonly Dictionary<string, List<(ushort Op, Type Type)>> _listByMethod = new();
        private static readonly object _lock = new();
        private static bool _bootstrapped;

        public static void DefineDto<T>(Fields fields) where T : new()
        {
            lock (_lock) _schemas[typeof(T)] = new Schema { Fields = fields };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool DefineStruct(ushort opcode, string method, Fields fields)
        {
            var t = new StackFrame(1, false).GetMethod()?.DeclaringType
                ?? throw new ProudException("DefineStruct: no se pudo inferir el tipo; llamalo desde 'static readonly bool __r = P.DefineStruct(...)' dentro de la clase del mensaje.");
            Register(t, opcode, method, fields);
            return true;
        }

        public static bool DefineStruct<T>(ushort opcode, string method, Fields fields) where T : new()
        {
            Register(typeof(T), opcode, method, fields);
            return true;
        }

        private static void Register(Type t, ushort opcode, string method, Fields fields)
        {
            lock (_lock)
            {
                _schemas[t] = new Schema { Fields = fields };
                _opByType[t] = opcode;
                if (!_byMethod.TryGetValue(method, out var map))
                    _byMethod[method] = map = new Dictionary<ushort, Type>();
                map[opcode] = t;
                if (!_listByMethod.TryGetValue(method, out var lst))
                    _listByMethod[method] = lst = new List<(ushort, Type)>();
                lst.Add((opcode, t));
            }
        }

        public static bool IsRegistered(Type t) => _schemas.ContainsKey(t);
        public static bool TryGetOpCode(Type t, out ushort op) => _opByType.TryGetValue(t, out op);

        private static readonly Dictionary<Type, PacketType?> _channelCache = new();
        public static PacketType? ChannelOf(object message)
        {
            if (message == null) return null;
            var t = message.GetType();
            lock (_channelCache)
            {
                if (_channelCache.TryGetValue(t, out var c)) return c;
                c = ((PacketAttribute)Attribute.GetCustomAttribute(t, typeof(PacketAttribute)))?.Type;
                _channelCache[t] = c;
                return c;
            }
        }

        public static IEnumerable<(ushort Op, Type Type)> TypesFor(string method)
        {
            Bootstrap();
            if (_listByMethod.TryGetValue(method, out var lst))
                foreach (var e in lst) yield return e;
        }

        public static void Bootstrap()
        {
            if (_bootstrapped) return;
            lock (_lock)
            {
                if (_bootstrapped) return;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract) continue;
                        var pa = (PacketAttribute)Attribute.GetCustomAttribute(t, typeof(PacketAttribute));
                        if (pa != null)
                            Register(t, pa.OpCode, Method(pa.Type), DeriveFields(t));
                        else if (Attribute.IsDefined(t, typeof(DtoAttribute)))
                            lock (_lock) _schemas[t] = new Schema { Fields = DeriveFields(t) };
                        else if (typeof(IMessage).IsAssignableFrom(t))
                            RuntimeHelpers.RunClassConstructor(t.TypeHandle);
                    }
                }
                _bootstrapped = true;
            }
        }

        private static string Method(PacketType t) => t switch
        {
            PacketType.GameRule => "gamerule",
            _ => t.ToString().ToLowerInvariant(),
        };

        private static Fields DeriveFields(Type t)
        {
            var acc = new List<(int order, string name, FieldType ft)>();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0) continue;
                if (p.IsDefined(typeof(SkipAttribute), false)) continue;
                acc.Add((p.MetadataToken, p.Name, ResolveToken(p.PropertyType, p)));
            }
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.IsInitOnly || f.IsLiteral || f.Name.IndexOf('<') >= 0) continue;
                if (f.IsDefined(typeof(SkipAttribute), false)) continue;
                acc.Add((f.MetadataToken, f.Name, ResolveToken(f.FieldType, f)));
            }
            acc.Sort((a, b) => a.order.CompareTo(b.order));
            var fields = new Fields();
            foreach (var (_, name, ft) in acc) fields.Add(name, ft);
            return fields;
        }

        private static FieldType ResolveToken(Type type, MemberInfo mi)
        {
            var wire = (WireAttribute)Attribute.GetCustomAttribute(mi, typeof(WireAttribute));
            if (wire != null) return new FieldType(wire.Kind);
            if (mi.IsDefined(typeof(CompressedAttribute), false)) return CompressedFloat;
            if (mi.IsDefined(typeof(IntBoolAttribute), false)) return IntBoolean;
            if (mi.IsDefined(typeof(EndpointStrAttribute), false)) return EndpointString;
            if (mi.IsDefined(typeof(SecAttribute), false)) return TimeSpanSeconds;
            var fx = (FixedAttribute)Attribute.GetCustomAttribute(mi, typeof(FixedAttribute));
            if (fx != null)
            {
                if (type.IsArray && type.GetElementType() != typeof(byte))
                {
                    var e = type.GetElementType();
                    return IsStructType(e) ? FixedArrayOf(fx.N, e) : FixedArrayOf(fx.N, Infer(e));
                }
                return FixedBytes(fx.N);
            }
            bool scalar = mi.IsDefined(typeof(ScalarAttribute), false);
            bool index = mi.IsDefined(typeof(IndexAttribute), false);
            if (scalar || index)
            {
                var elem = ElementType(type);
                var kind = scalar ? Kind.ArrayScalar : Kind.ArrayIntIndex;
                return IsStructType(elem) ? new FieldType(kind, structType: elem) : new FieldType(kind, elem: Infer(elem));
            }
            return Infer(type);
        }

        private static FieldType Infer(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            if (t.IsEnum) t = Enum.GetUnderlyingType(t);
            if (t == typeof(bool)) return Bool;
            if (t == typeof(byte)) return Byte;
            if (t == typeof(sbyte)) return SByte;
            if (t == typeof(short)) return Short;
            if (t == typeof(ushort)) return UShort;
            if (t == typeof(int)) return Int;
            if (t == typeof(uint)) return UInt;
            if (t == typeof(long)) return Long;
            if (t == typeof(ulong)) return ULong;
            if (t == typeof(float)) return Float;
            if (t == typeof(double)) return Double;
            if (t == typeof(string)) return Str;
            if (t == typeof(Guid)) return Guid;
            if (t == typeof(Version)) return Version;
            if (t == typeof(DateTimeOffset)) return UnixTime;
            if (t == typeof(TimeSpan)) return TimeSpanMs;
            if (t == typeof(IPEndPoint)) return Endpoint;
            if (t == typeof(Vector3)) return CompressedVector;
            if (t == typeof(Vector2)) return RotationVector;
            if (t == typeof(Color)) return ColorArgb;
            if (t.IsArray) { var e = t.GetElementType(); return IsStructType(e) ? new FieldType(Kind.ArrayInt, structType: e) : new FieldType(Kind.ArrayInt, elem: Infer(e)); }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) { var e = t.GetGenericArguments()[0]; return IsStructType(e) ? new FieldType(Kind.ArrayInt, structType: e) : new FieldType(Kind.ArrayInt, elem: Infer(e)); }
            var prim = WrapperPrimitive(t);
            if (prim != null) return prim;
            return Struct(t);
        }

        private static Type ElementType(Type t) =>
            t.IsArray ? t.GetElementType() : (t.IsGenericType ? t.GetGenericArguments()[0] : typeof(object));

        private static FieldType WrapperPrimitive(Type t)
        {
            if (t.IsPrimitive || t.IsEnum || t == typeof(string)) return null;
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if ((m.Name != "op_Implicit" && m.Name != "op_Explicit")) continue;
                var ps = m.GetParameters();
                if (ps.Length != 1 || ps[0].ParameterType != t) continue;
                var r = m.ReturnType;
                if (r == typeof(uint)) return UInt;
                if (r == typeof(ulong)) return ULong;
                if (r == typeof(ushort)) return UShort;
                if (r == typeof(int)) return Int;
                if (r == typeof(long)) return Long;
                if (r == typeof(short)) return Short;
                if (r == typeof(byte)) return Byte;
            }
            return null;
        }

        private static bool IsStructType(Type t)
        {
            if (t == null) return false;
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(Guid)) return false;
            if (t == typeof(Version) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan) || t == typeof(IPEndPoint)
                || t == typeof(Vector3) || t == typeof(Vector2) || t == typeof(Color)) return false;
            if (WrapperPrimitive(t) != null) return false;
            return t.IsClass;
        }

        public static void Serialize(BinaryWriter w, object message)
        {
            if (message != null && _schemas.TryGetValue(message.GetType(), out var schema))
            {
                foreach (var (name, type) in schema.Fields)
                    WriteField(w, type, GetMember(message.GetType(), name).Get(message));
                return;
            }
            SantanaLib.Serialization.Serializer.Serialize(w, message);
        }

        public static object Deserialize(BinaryReader r, Type type)
        {
            if (_schemas.TryGetValue(type, out var schema))
            {
                var obj = Activator.CreateInstance(type);
                foreach (var (name, ft) in schema.Fields)
                {
                    var m = GetMember(type, name);
                    m.Set(obj, ReadField(r, ft, m.Type));
                }
                return obj;
            }
            return SantanaLib.Serialization.Serializer.Deserialize(r, type);
        }

        public static object Deserialize(Stream s, Type type)
        {
            if (_schemas.ContainsKey(type))
                using (var r = new BinaryReader(s, System.Text.Encoding.UTF8, true))
                    return Deserialize(r, type);
            return SantanaLib.Serialization.Serializer.Deserialize(s, type);
        }

        private static readonly Dictionary<Kind, Type> _primClr = new()
        {
            { Kind.Bool, typeof(bool) }, { Kind.Byte, typeof(byte) }, { Kind.SByte, typeof(sbyte) },
            { Kind.Short, typeof(short) }, { Kind.UShort, typeof(ushort) }, { Kind.Int, typeof(int) },
            { Kind.UInt, typeof(uint) }, { Kind.Long, typeof(long) }, { Kind.ULong, typeof(ulong) },
            { Kind.Float, typeof(float) }, { Kind.Double, typeof(double) },
            { Kind.RoomLocation, typeof(uint) }, { Kind.IntBoolean, typeof(bool) },
        };

        private static void WriteField(BinaryWriter w, FieldType ft, object v)
        {
            if (v != null && v is not IConvertible && _primClr.TryGetValue(ft.Kind, out var clr))
                v = Unwrap(v, clr);
            switch (ft.Kind)
            {
                case Kind.Bool: w.Write(Convert.ToBoolean(v ?? false)); return;
                case Kind.Byte: w.Write(Convert.ToByte(v ?? (byte)0)); return;
                case Kind.SByte: w.Write(Convert.ToSByte(v ?? (sbyte)0)); return;
                case Kind.Short: w.Write(Convert.ToInt16(v ?? (short)0)); return;
                case Kind.UShort: w.Write(Convert.ToUInt16(v ?? (ushort)0)); return;
                case Kind.Int: w.Write(Convert.ToInt32(v ?? 0)); return;
                case Kind.UInt: w.Write(Convert.ToUInt32(v ?? 0u)); return;
                case Kind.Long: w.Write(Convert.ToInt64(v ?? 0L)); return;
                case Kind.ULong: w.Write(Convert.ToUInt64(v ?? 0UL)); return;
                case Kind.Float: w.Write(Convert.ToSingle(v ?? 0f)); return;
                case Kind.Double: w.Write(Convert.ToDouble(v ?? 0d)); return;
                case Kind.Str: w.WriteProudString((string)(v ?? string.Empty), false); return;
                case Kind.Guid: w.Write(((Guid)(v ?? System.Guid.Empty)).ToByteArray()); return;
                case Kind.Endpoint: w.Write(v as IPEndPoint); return;
                case Kind.EndpointString:
                {
                    var ep = v as IPEndPoint;
                    w.WriteProudString(ep != null ? ep.Address.ToString() : "255.255.255.255", false);
                    w.Write(ep != null ? (ushort)ep.Port : (ushort)0);
                    return;
                }
                case Kind.RoomLocation: w.Write(Convert.ToUInt32(v ?? 0u)); return;
                case Kind.IntBoolean: w.Write(Convert.ToBoolean(v ?? false) ? 1 : 0); return;
                case Kind.UnixTime: w.Write(((DateTimeOffset)(v ?? DateTimeOffset.MinValue)).ToUnixTimeSeconds()); return;
                case Kind.TimeSpanMs: w.Write((uint)((TimeSpan)(v ?? TimeSpan.Zero)).TotalMilliseconds); return;
                case Kind.TimeSpanSeconds: w.Write((uint)((TimeSpan)(v ?? TimeSpan.Zero)).TotalSeconds); return;
                case Kind.Version:
                {
                    var ver = v as Version ?? new Version(0, 0, 0, 0);
                    w.Write(4);
                    w.Write((ushort)Math.Max(0, ver.Major)); w.Write((ushort)Math.Max(0, ver.Minor));
                    w.Write((ushort)Math.Max(0, ver.Build)); w.Write((ushort)Math.Max(0, ver.Revision));
                    return;
                }
                case Kind.CompressedFloat: w.Write(CompressF(Convert.ToSingle(v ?? 0f))); return;
                case Kind.CompressedVector:
                {
                    var vec = (Vector3)(v ?? Vector3.Zero);
                    w.Write(CompressF(vec.X)); w.Write(CompressF(vec.Y)); w.Write(CompressF(vec.Z)); return;
                }
                case Kind.RotationVector:
                {
                    var vec = (Vector2)(v ?? Vector2.Zero);
                    w.Write((byte)vec.X); w.Write((byte)vec.Y); return;
                }
                case Kind.Color: w.Write(((Color)(v ?? Color.Empty)).ToArgb()); return;
                case Kind.FixedBytes:
                {
                    var bytes = ToByteArray(v);
                    for (int i = 0; i < ft.FixedLen; i++) w.Write(i < bytes.Length ? bytes[i] : (byte)0);
                    return;
                }
                case Kind.ArrayInt:
                case Kind.ArrayScalar:
                case Kind.ArrayIntIndex:
                {
                    var items = (v as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
                    if (ft.Kind == Kind.ArrayScalar) w.WriteScalar(items.Count);
                    else w.Write(items.Count);
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (ft.Kind == Kind.ArrayIntIndex) w.Write((byte)i);
                        if (ft.StructType != null) SerializeInto(w, items[i], ft.StructType);
                        else WriteField(w, ft.Elem, items[i]);
                    }
                    return;
                }
                case Kind.FixedArray:
                {
                    var items = (v as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
                    for (int i = 0; i < ft.FixedLen; i++)
                    {
                        var item = i < items.Count ? items[i] : null;
                        if (ft.StructType != null) SerializeInto(w, item, ft.StructType);
                        else WriteField(w, ft.Elem, item);
                    }
                    return;
                }
                case Kind.Struct: SerializeInto(w, v, ft.StructType); return;
            }
        }

        private static object ReadField(BinaryReader r, FieldType ft, Type memberType)
        {
            switch (ft.Kind)
            {
                case Kind.Bool: return r.ReadBoolean();
                case Kind.Byte: return r.ReadByte();
                case Kind.SByte: return r.ReadSByte();
                case Kind.Short: return ConvertTo(r.ReadInt16(), memberType);
                case Kind.UShort: return ConvertTo(r.ReadUInt16(), memberType);
                case Kind.Int: return ConvertTo(r.ReadInt32(), memberType);
                case Kind.UInt: return ConvertTo(r.ReadUInt32(), memberType);
                case Kind.Long: return ConvertTo(r.ReadInt64(), memberType);
                case Kind.ULong: return ConvertTo(r.ReadUInt64(), memberType);
                case Kind.Float: return r.ReadSingle();
                case Kind.Double: return r.ReadDouble();
                case Kind.Str: return r.ReadProudString();
                case Kind.Guid: return new Guid(r.ReadBytes(16));
                case Kind.Endpoint: return r.ReadIPEndPoint();
                case Kind.EndpointString:
                {
                    var addr = r.ReadProudString();
                    return new IPEndPoint(IPAddress.Parse(addr), r.ReadUInt16());
                }
                case Kind.RoomLocation: return ConvertTo(r.ReadUInt32(), memberType);
                case Kind.IntBoolean: return r.ReadInt32() != 0;
                case Kind.UnixTime: return DateTimeOffset.FromUnixTimeSeconds(r.ReadInt64());
                case Kind.TimeSpanMs: return TimeSpan.FromMilliseconds(r.ReadUInt32());
                case Kind.TimeSpanSeconds: return TimeSpan.FromSeconds(r.ReadUInt32());
                case Kind.Version:
                {
                    int count = r.ReadInt32();
                    SecurityGuard.EnsureArrayLength(count, "Packet.Version");
                    var parts = new int[4];
                    for (int i = 0; i < count; i++) { var p = r.ReadUInt16(); if (i < 4) parts[i] = p; }
                    return new Version(parts[0], parts[1], parts[2], parts[3]);
                }
                case Kind.CompressedFloat: return DecompressF(r.ReadInt16());
                case Kind.CompressedVector: return new Vector3(DecompressF(r.ReadInt16()), DecompressF(r.ReadInt16()), DecompressF(r.ReadInt16()));
                case Kind.RotationVector: return new Vector2(r.ReadByte(), r.ReadByte());
                case Kind.Color: return Color.FromArgb(r.ReadInt32());
                case Kind.FixedBytes: return r.ReadBytes(ft.FixedLen);
                case Kind.ArrayInt:
                case Kind.ArrayScalar:
                case Kind.ArrayIntIndex:
                {
                    int count = ft.Kind == Kind.ArrayScalar ? r.ReadScalar() : r.ReadInt32();
                    SecurityGuard.EnsureArrayLength(count, "Packet");
                    var elemType = memberType.IsArray ? memberType.GetElementType()
                        : (memberType.IsGenericType ? memberType.GetGenericArguments()[0] : typeof(object));
                    var arr = Array.CreateInstance(elemType, Math.Max(0, count));
                    for (int i = 0; i < count; i++)
                    {
                        if (ft.Kind == Kind.ArrayIntIndex) r.ReadByte();
                        object item = ft.StructType != null ? DeserializeInto(r, ft.StructType) : ReadField(r, ft.Elem, elemType);
                        arr.SetValue(ConvertTo(item, elemType), i);
                    }
                    return arr;
                }
                case Kind.FixedArray:
                {
                    var elemType = memberType.IsArray ? memberType.GetElementType()
                        : (memberType.IsGenericType ? memberType.GetGenericArguments()[0] : typeof(object));
                    var arr = Array.CreateInstance(elemType, ft.FixedLen);
                    for (int i = 0; i < ft.FixedLen; i++)
                    {
                        object item = ft.StructType != null ? DeserializeInto(r, ft.StructType) : ReadField(r, ft.Elem, elemType);
                        arr.SetValue(ConvertTo(item, elemType), i);
                    }
                    return arr;
                }
                case Kind.Struct: return DeserializeInto(r, ft.StructType);
            }
            return null;
        }

        private static void SerializeInto(BinaryWriter w, object v, Type t)
        {
            if (_schemas.TryGetValue(t, out var schema))
            {
                v ??= Activator.CreateInstance(t);
                foreach (var (name, ft) in schema.Fields)
                    WriteField(w, ft, GetMember(t, name).Get(v));
            }
            else SantanaLib.Serialization.Serializer.Serialize(w, v ?? Activator.CreateInstance(t));
        }

        [ThreadStatic] private static int _deserializeDepth;
        private static object DeserializeInto(BinaryReader r, Type t)
        {
            if (++_deserializeDepth > 32)
            {
                _deserializeDepth--;
                throw new ProudException("Packet: nested struct depth exceeds 32");
            }
            try
            {
                if (_schemas.TryGetValue(t, out var schema))
                {
                    var obj = Activator.CreateInstance(t);
                    foreach (var (name, ft) in schema.Fields)
                    {
                        var m = GetMember(t, name);
                        m.Set(obj, ReadField(r, ft, m.Type));
                    }
                    return obj;
                }
                return SantanaLib.Serialization.Serializer.Deserialize(r, t);
            }
            finally
            {
                _deserializeDepth--;
            }
        }

        private static short CompressF(float value)
        {
            var tmp = (uint)BitConverter.SingleToInt32Bits(value);
            var v1 = (tmp >> 31) & 0x1;
            var v2 = (int)((tmp >> 23) & 0xff) - 127;
            var v3 = tmp & 0x7fffff;
            return (short)(((v1 << 15) | (uint)((v2 + 7) << 9) | (v3 >> 14)) & 0xffff);
        }
        private static float DecompressF(short s)
        {
            int result = ((s & 0x1ff) << 14) | ((((s & 0x7f00) >> 9) - 7 + 127) << 23) | (((s & 0x8000) >> 15) << 31);
            return BitConverter.Int32BitsToSingle(result);
        }

        private sealed class Member
        {
            public Type Type; public Func<object, object> Get; public Action<object, object> Set;
        }
        private static readonly Dictionary<(Type, string), Member> _members = new();
        private static Member GetMember(Type owner, string name)
        {
            var key = (owner, name);
            if (_members.TryGetValue(key, out var m)) return m;
            lock (_members)
            {
                if (_members.TryGetValue(key, out m)) return m;
                var f = owner.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) m = new Member { Type = f.FieldType, Get = f.GetValue, Set = f.SetValue };
                else
                {
                    var p = owner.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                        ?? throw new ProudException($"Packet: no field/prop '{name}' en {owner.FullName}");
                    m = new Member { Type = p.PropertyType, Get = p.GetValue, Set = p.SetValue };
                }
                _members[key] = m;
                return m;
            }
        }

        private static object ConvertTo(object v, Type target)
        {
            if (v == null) return null;
            var t = Nullable.GetUnderlyingType(target) ?? target;
            if (t.IsInstanceOfType(v)) return v;
            if (t.IsEnum) return Enum.ToObject(t, v);
            var op = t.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, null, new[] { v.GetType() }, null)
                  ?? t.GetMethod("op_Explicit", BindingFlags.Public | BindingFlags.Static, null, new[] { v.GetType() }, null);
            if (op != null && op.ReturnType == t) return op.Invoke(null, new[] { v });
            var ctor = t.GetConstructor(new[] { v.GetType() });
            if (ctor != null) return ctor.Invoke(new[] { v });
            try { return Convert.ChangeType(v, t); } catch { return v; }
        }
        private static object Unwrap(object v, Type clrTarget)
        {
            if (v == null) return null;
            if (clrTarget.IsInstanceOfType(v)) return v;
            foreach (var m in v.GetType().GetMethods(BindingFlags.Public | BindingFlags.Static))
                if ((m.Name == "op_Implicit" || m.Name == "op_Explicit") && m.ReturnType == clrTarget
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == v.GetType())
                    return m.Invoke(null, new[] { v });
            var vp = v.GetType().GetProperty("Value");
            return vp != null ? vp.GetValue(v) : v;
        }
        private static byte[] ToByteArray(object v)
        {
            if (v is byte[] b) return b;
            if (v is IEnumerable en) return en.Cast<object>().Select(x => Convert.ToByte(x)).ToArray();
            return Array.Empty<byte>();
        }
    }

}
