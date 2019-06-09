﻿using System.Collections.Generic;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace SoMeta.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        public override IEnumerable<string> GetAssembliesForScanning()
        {
            return new[] { "netstandard", "mscorlib" };
        }

        public override void Execute()
        {
            var soMeta = ModuleDefinition.FindAssembly("SoMeta");

            CecilExtensions.LogInfo = LogInfo;
            CecilExtensions.LogWarning = LogWarning;
            CecilExtensions.LogError = LogError;
            CecilExtensions.TypeSystem = TypeSystem;
            CecilExtensions.Initialize(ModuleDefinition, soMeta);

            // Inventory candidate classes
            var propertyInterceptorAttribute = ModuleDefinition.FindType("SoMeta", "PropertyInterceptorAttribute", soMeta);
            var methodInterceptorAttribute = ModuleDefinition.FindType("SoMeta", "MethodInterceptorAttribute", soMeta);

            var propertyInterceptions = new List<(PropertyDefinition, CustomAttribute)>();
            var methodInterceptions = new List<(MethodDefinition, CustomAttribute)>();

            var propertyInterceptorWeaver = new PropertyInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, propertyInterceptorAttribute);
            var methodInterceptorWeaver = new MethodInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, methodInterceptorAttribute);

            foreach (var type in ModuleDefinition.GetAllTypes())
            {
                foreach (var property in type.Properties)
                {
                    var interceptor = property.GetCustomAttributesInAncestry(propertyInterceptorAttribute).SingleOrDefault();
                    if (interceptor != null)
                    {
                        LogInfo($"Discovered property interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                        propertyInterceptions.Add((property, interceptor));
                    }
                }
                foreach (var method in type.Methods)
                {
                    var interceptor = method.GetCustomAttributesInAncestry(methodInterceptorAttribute).SingleOrDefault();
                    if (interceptor != null)
                    {
                        LogInfo($"Discovered method interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                        methodInterceptions.Add((method, interceptor));
                    }
                }
            }

            foreach (var (property, interceptor) in propertyInterceptions)
            {
                propertyInterceptorWeaver.Weave(property, interceptor);
            }

            foreach (var (method, interceptor) in methodInterceptions)
            {
                methodInterceptorWeaver.Weave(method, interceptor);
            }
        }
    }
}