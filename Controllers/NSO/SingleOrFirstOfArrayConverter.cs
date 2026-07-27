using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vendor_Application_MVC.Controllers.NSO
{
    /// <summary>
    /// Binds either a JSON object or a JSON array to a single-object property.
    ///
    /// Legacy and raw-RFC callers send ET_HEAD / ET_ADD as arrays because the
    /// underlying RFC parameters are tables (ZECOMM_ORD_HEAD_TT / _ADD_TT).
    /// ZECOMM_ORDER_POST_RFC only ever reads INDEX 1, so an array is collapsed
    /// to its first element — the same result the FM would produce. Object
    /// payloads take an identical path to before and are unaffected.
    ///
    /// Applies to both vendor routes (ZECOMM_ORDER_POST_UT / _GH): they share
    /// one request model, so both accept both shapes.
    /// </summary>
    public class SingleOrFirstOfArrayConverter<T> : JsonConverter where T : class
    {
        public override bool CanConvert(Type objectType) { return objectType == typeof(T); }
        public override bool CanWrite { get { return false; } }

        public override object ReadJson(JsonReader reader, Type objectType,
                                        object existingValue, JsonSerializer serializer)
        {
            // Buffering through JToken would otherwise parse bare JSON numbers as
            // Double (JToken.Load's default), silently truncating high-precision
            // values before they reach the decimal? properties. Force decimal so
            // object payloads bind bit-identically to direct deserialization.
            FloatParseHandling previous = reader.FloatParseHandling;
            reader.FloatParseHandling = FloatParseHandling.Decimal;
            JToken token;
            try { token = JToken.Load(reader); }
            finally { reader.FloatParseHandling = previous; }

            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;

                case JTokenType.Object:
                    return token.ToObject<T>(serializer);

                case JTokenType.Array:
                    JArray arr = (JArray)token;
                    if (arr.Count == 0) return null;
                    // FM reads INDEX 1 only; rows 2+ were always discarded.
                    return arr[0].Type == JTokenType.Object
                         ? arr[0].ToObject<T>(serializer)
                         : null;

                default:
                    throw new JsonSerializationException(
                        "Expected object or array for " + typeof(T).Name + ".");
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }
}
