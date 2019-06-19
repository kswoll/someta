using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Someta.Fody
{
    public class StateWeaver : BaseWeaver
    {
        private readonly TypeReference injectedFieldType;
        private readonly TypeReference injectFieldAttributeType;

        public StateWeaver(WeaverContext context) : base(context)
        {
//            Debugger.Launch();
            injectedFieldType = ModuleDefinition.FindType("Someta", "InjectedField`1", Context.Someta, "T");
            injectFieldAttributeType = ModuleDefinition.FindType("Someta", "InjectFieldAttribute", Context.Someta);
        }

        public void Weave(IMemberDefinition member, ExtensionPointAttribute extensionPoint)
        {
            var type = member is TypeDefinition definition ? definition : member.DeclaringType;
            var attributeField = CacheAttributeInstance(member, extensionPoint);

            var attributeType = extensionPoint.AttributeType.Resolve();
            foreach (var property in attributeType.Properties)
            {
                if (property.PropertyType.IsGenericInstance && property.PropertyType.GetElementType().CompareTo(injectedFieldType))
                {
                    var propertyType = ((GenericInstanceType)property.PropertyType).GenericArguments[0];
                    var fieldName = GenerateUniqueName(member, attributeType, property.Name);
                    var injectFieldAttribute = property.GetCustomAttributes(injectFieldAttributeType).SingleOrDefault();
                    var isStatic = injectFieldAttribute != null && (bool)injectFieldAttribute.ConstructorArguments[0].Value;
                    var fieldAttributes = FieldAttributes.Private;
                    if (isStatic)
                        fieldAttributes |= FieldAttributes.Static;
                    var field = new FieldDefinition(fieldName, fieldAttributes, propertyType);
                    type.Fields.Add(field);

                    // Generate accessors
                    var getMethodName = GenerateUniqueName(member, attributeType, $"{property.Name}Getter");
                    var getMethod = new MethodDefinition(getMethodName, MethodAttributes.Private | MethodAttributes.Static, propertyType);
                    getMethod.Parameters.Add(new ParameterDefinition(TypeSystem.ObjectReference));
                    getMethod.Body = new MethodBody(getMethod);
                    getMethod.Body.InitLocals = true;
                    getMethod.Body.Emit(il =>
                    {
                        if (!isStatic)
                        {
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Castclass, type);
                        }
                        il.LoadField(field);
                        il.Emit(OpCodes.Ret);
                    });
                    type.Methods.Add(getMethod);

                    var setMethodName = GenerateUniqueName(member, attributeType, $"{property.Name}Setter");
                    var setMethod = new MethodDefinition(setMethodName, MethodAttributes.Private | MethodAttributes.Static, TypeSystem.VoidReference);
                    setMethod.Parameters.Add(new ParameterDefinition(TypeSystem.ObjectReference));
                    setMethod.Parameters.Add(new ParameterDefinition(propertyType));
                    setMethod.Body = new MethodBody(setMethod);
                    setMethod.Body.InitLocals = true;
                    setMethod.Body.Emit(il =>
                    {
                        if (!isStatic)
                        {
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Castclass, type);
                        }
                        il.Emit(OpCodes.Ldarg_1);
                        il.SaveField(field);
                        il.Emit(OpCodes.Ret);
                    });
                    type.Methods.Add(setMethod);

                    type.EmitToStaticConstructor(il =>
                    {
                        var genericInjectedFieldType = injectedFieldType.MakeGenericInstanceType(propertyType);
                        var injectedFieldConstructor = ModuleDefinition.FindConstructor(genericInjectedFieldType).Bind(genericInjectedFieldType);

                        il.LoadField(attributeField);       // Instance on which to set the InjectedField

                        // Instantiate the InjectedField
                        il.EmitLocalMethodDelegate(getMethod, Context.Func2Type, TypeSystem.ObjectReference, propertyType);
                        il.EmitLocalMethodDelegate(setMethod, Context.ActionTypes[2], TypeSystem.ObjectReference, propertyType);
                        il.Emit(OpCodes.Newobj, injectedFieldConstructor);

                        il.EmitCall(property.SetMethod);
                    });
                }
            }
        }
    }
}