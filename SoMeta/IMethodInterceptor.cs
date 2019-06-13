﻿using System;
using System.Reflection;

namespace Someta
{
    public interface IMethodInterceptor : IInterceptor
    {
        object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker);
    }
}