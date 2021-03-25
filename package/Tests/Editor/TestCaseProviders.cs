using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class PatchingTestCase
{
    public readonly MethodInfo methodInfo;
        
    public PatchingTestCase(MethodInfo methodInfo)
    {
        this.methodInfo = methodInfo;
    }

    public PatchingTestCase(Type type, string methodName) : this(TestHelpers.GetMethodInfo(type, methodName))
    {
            
    }

    public override string ToString()
    {
        return methodInfo?.DeclaringType?.FullName + "." + methodInfo;
    }
}

public class NamespaceTestCase
{
    public string Namespace { get; }
    public IEnumerable<Type> Types { get; }
        
    public NamespaceTestCase(string nameSpace, IEnumerable<Type> namespaceToType)
    {
        this.Namespace = nameSpace;
        this.Types = namespaceToType;
    }

    public override string ToString()
    {
        return Namespace + " (" + Types.Count() + " types)";
    }
}
