using Mono.Cecil;

namespace SoMeta.Fody
{
    public struct InterceptorAttribute
    {
        public IMemberDefinition DeclaringMember { get; }
        public CustomAttribute Attribute { get; }
        public TypeReference AttributeType => Attribute.AttributeType;
        public int Index { get; }
        public InterceptorScope Scope { get; }

        public InterceptorAttribute(IMemberDefinition declaringMember, CustomAttribute attribute, int index, InterceptorScope scope) : this()
        {
            DeclaringMember = declaringMember;
            Attribute = attribute;
            Index = index;
            Scope = scope;
        }
    }
}