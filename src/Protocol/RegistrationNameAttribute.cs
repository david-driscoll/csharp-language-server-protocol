using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MediatR;

namespace OmniSharp.Extensions.LanguageServer.Protocol
{
    public class RegistrationNameAttribute  : Attribute
    {
        public string Method { get; }

        public RegistrationNameAttribute(string method) => Method = method;

        public static RegistrationNameAttribute? From(Type? type) => AllFrom(type).FirstOrDefault();

        public static IEnumerable<RegistrationNameAttribute> AllFrom(Type? type) =>
            CollectRegistrationNameAttributes(type)
               .Concat(
                    type
                      ?.GetInterfaces()
                       .SelectMany(CollectRegistrationNameAttributes)
                 ?? Enumerable.Empty<RegistrationNameAttribute>()
                );

        private static IEnumerable<RegistrationNameAttribute> CollectRegistrationNameAttributes(Type? type)
        {
            if (type == null) return Enumerable.Empty<RegistrationNameAttribute>();
            if (type.IsGenericType && typeof(IRequestHandler<,>) == type.GetGenericTypeDefinition())
            {
                return type.GetTypeInfo().GetCustomAttributes<RegistrationNameAttribute>(true).Concat(AllFrom(type.GetGenericArguments()[0]));
            }

            return type.GetTypeInfo().GetCustomAttributes<RegistrationNameAttribute>(true);
        }
    }
}
