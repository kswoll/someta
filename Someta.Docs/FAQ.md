# Someta

What is this library?  It aims to be a swiss-army knife toolkit for easy meta programming in C# built on the back of the [Fody](https://github.com/Fody/Fody) engine.

### Why not just use Fody directly?
You can!  It's a great library.  But it requires you to jump through a number of hoops that you usually don't want to deal with.  Besides the fact that it requires you to be well versed in IL and stack based assembly semantics, it also requires the instrumentation code ("weavers") to be declared in a separate project that requires additional setup.

This library is here to give you the ability to create your own weavers in your existing projects with minimal fuss.

### Wait, isn't this what [Cauldron.Fody](https://github.com/Capgemini/Cauldron/tree/master/Fody) already does?

Yes, to a certain extent.  But it is now in maintenance mode, and the implementation lacks a certain degree of intuitive freedom that this library hopes to address.  One example, their interceptors don't provide a facility to simply "proceed" with the original implementation.  Instead, it requires you to target specific interception points (beginning of method, end of method, before property changed, after property changed, handling exceptions, etc.).  With Someta, you get access to a delegate that allows you to deal with all of these scenarios in a simple and clean way.

### How does Someta compare to [MethodBoundaryAspect.Fody](https://github.com/vescon/MethodBoundaryAspect.Fody)?

This (I think) is a partial fork of what was in Cauldron.Fody but either way has the exact same contract.  Consider the docs (see link) around changing the return value, changing the arguments, handling exceptions, and async behavior.  Now compare that to:

```
public object Invoke(MethodInfo methodInfo, object instance, Type[] typeArguments, object[] arguments, Func<object[], object> invoker)
{
    try 
    {
        return invoker(arguments);
    }
    catch (Exception ex)
    {
        Logger.Log(ex); // just an example
    }
}
```

How to change the return value and all that is pretty much exactly what you'd expect in C#.
