using System.Reflection;

namespace Bruinet.Bdd;

// Calls a method found by reflection, passing what the caller has and Type.Missing for the rest.
internal static class BddInvoker
{
    internal static bool TryInvokeBool(object instance, string methodName, object? argument, out bool result)
    {
        result = false;
        var method = FindInvokableMethod(instance.GetType(), methodName, argument is null ? [] : [argument]);
        if (method is null)
        {
            return false;
        }

        var value = Invoke(instance, method, argument);
        if (value is bool boolean)
        {
            result = boolean;
            return true;
        }

        return false;
    }

    internal static bool TryInvokeBool(object instance, string methodName, object? first, object? second, out bool result)
    {
        result = false;
        var method = FindInvokableMethod(instance.GetType(), methodName, [first, second]);
        if (method is null)
        {
            return false;
        }

        var value = Invoke(instance, method, first, second);
        if (value is bool boolean)
        {
            result = boolean;
            return true;
        }

        return false;
    }

    internal static object? Invoke(object instance, string methodName, params object?[] providedArguments)
    {
        var method = FindInvokableMethod(instance.GetType(), methodName, providedArguments);
        if (method is null)
        {
            throw new MissingMethodException(instance.GetType().FullName, methodName);
        }

        return Invoke(instance, method, providedArguments);
    }

    // A break for the next step definition called (Debug from here, AD-022).
    private static BddStepBreak? _definitionBreak;

    // The next step definition called breaks in the debugger, just before it runs.
    internal static void ArmDefinitionBreak(BddStepBreak stepBreak) => Volatile.Write(ref _definitionBreak, stepBreak);

    // A step ended without calling a definition: its armed break goes.
    internal static void DisarmDefinitionBreak() => Volatile.Write(ref _definitionBreak, null);

    // A step's own definition (a control's verb, a page's or a phrase class's method), breaking first when armed.
    internal static object? InvokeDefinition(object instance, string methodName, params object?[] providedArguments)
    {
        var method = FindInvokableMethod(instance.GetType(), methodName, providedArguments)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return InvokeDefinition(instance, method, providedArguments);
    }

    internal static object? InvokeDefinition(object instance, MethodInfo method, params object?[] providedArguments)
    {
        var arguments = Arguments(method, providedArguments);
        var stepBreak = Interlocked.Exchange(ref _definitionBreak, null);
        try
        {
            if (stepBreak is not null && System.Diagnostics.Debugger.IsAttached)
            {
                // Debug from here: stepBreak is the step. F11 on the call below goes into its definition.
                System.Diagnostics.Debugger.Break();
            }

            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Its own stack kept: the step definition's and the app's frames, where it was thrown.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    internal static object? Invoke(object instance, MethodInfo method, params object?[] providedArguments)
    {
        var arguments = Arguments(method, providedArguments);
        try
        {
            var result = method.Invoke(instance, arguments);
            return result;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Its own stack kept: the step definition's and the app's frames, where it was thrown.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    // What a method is called with: the arguments given, Type.Missing for the optional rest.
    private static object?[] Arguments(MethodInfo method, object?[] providedArguments)
    {
        var parameters = method.GetParameters();
        object?[] arguments = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            arguments[i] = i < providedArguments.Length
                ? providedArguments[i]
                : Type.Missing;
        }

        return arguments;
    }

    internal static MethodInfo? FindInvokableMethod(Type type, string methodName, IReadOnlyList<object?> providedArguments)
    {
        return type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name.Equals(methodName, StringComparison.Ordinal))
            .FirstOrDefault(method =>
            {
                var parameters = method.GetParameters();
                if (parameters.Count(parameter => !parameter.IsOptional) > providedArguments.Count ||
                    parameters.Length < providedArguments.Count)
                {
                    return false;
                }

                for (var i = 0; i < providedArguments.Count; i++)
                {
                    if (!CanPassArgument(providedArguments[i], parameters[i].ParameterType))
                    {
                        return false;
                    }
                }

                return true;
            });
    }

    internal static bool CanPassArgument(object? argument, Type parameterType)
    {
        var nullableInnerType = Nullable.GetUnderlyingType(parameterType);
        if (argument is null)
        {
            return nullableInnerType is not null || !parameterType.IsValueType;
        }

        var effectiveParameterType = nullableInnerType ?? parameterType;
        return effectiveParameterType.IsInstanceOfType(argument);
    }
}
