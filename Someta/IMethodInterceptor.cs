﻿using System;
using System.Reflection;

namespace Someta
{
    public interface IMethodInterceptor : IExtensionPoint
    {
        object Invoke(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], object> invoker);
    }
}