using System.Net;
using System.Reflection;

namespace BCDev.BC;

/// <summary>
/// Client context for connecting to Business Central.
/// Uses late binding to avoid compile-time dependency on BC client DLL.
/// </summary>
public class ClientContext : IDisposable
{
    protected dynamic ClientSession { get; private set; } = null!;
    protected string Culture { get; private set; } = "en-US";
    internal dynamic? OpenedForm { get; private set; }
    protected string OpenedFormName { get; private set; } = "";
    private dynamic? _caughtForm;
    protected bool IgnoreErrors { get; private set; } = true;

    // Cached enum values for performance
    private static object? _stateReady;
    private static object? _stateBusy;
    private static object? _stateInError;
    private static object? _stateTimedOut;
    private static object? _stateUninitialized;

    public string SessionId
    {
        get
        {
            if (ClientSession?.Info == null)
                return "";
            return ClientSession.Info.SessionId;
        }
    }

    public ClientContext(string serviceUrl, string authenticationScheme, ICredentials credential,
        TimeSpan interactionTimeout, string culture = "en-US")
    {
        Initialize(serviceUrl, authenticationScheme, credential, interactionTimeout, culture);
    }

    private void Initialize(string serviceUrl, string authenticationScheme, ICredentials credential,
        TimeSpan interactionTimeout, string culture)
    {
        // Ensure BC client is loaded (downloads if needed)
        BCClientLoader.EnsureLoadedAsync().GetAwaiter().GetResult();

        // Cache enum values
        _stateReady ??= BCClientLoader.GetSessionState("Ready");
        _stateBusy ??= BCClientLoader.GetSessionState("Busy");
        _stateInError ??= BCClientLoader.GetSessionState("InError");
        _stateTimedOut ??= BCClientLoader.GetSessionState("TimedOut");
        _stateUninitialized ??= BCClientLoader.GetSessionState("Uninitialized");

        // Configure keep-alive for long-running operations
        ServicePointManager.SetTcpKeepAlive(true,
            (int)TimeSpan.FromMinutes(120).TotalMilliseconds,
            (int)TimeSpan.FromSeconds(10).TotalMilliseconds);

        // Disable SSL verification for dev environments
        SslVerification.Disable();

        var clientServicesUrl = EnsureClientServicesPath(serviceUrl);
        var addressUri = new Uri(clientServicesUrl);

        // Get AuthenticationScheme enum value
        var authScheme = BCClientLoader.GetEnumValue("AuthenticationScheme", authenticationScheme);

        // Create JsonHttpClient using reflection to handle different constructor signatures
        var jsonClientType = BCClientLoader.GetClientType("JsonHttpClient");
        dynamic jsonClient = CreateJsonHttpClient(jsonClientType, addressUri, credential, authScheme);

        // Set timeout on the HTTP client via reflection
        var httpClientField = jsonClientType.GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new Exception("Could not find httpClient field");
        var httpClient = httpClientField.GetValue(jsonClient) as HttpClient
            ?? throw new Exception("Could not get httpClient instance");
        httpClient.Timeout = interactionTimeout;

        // Create NonDispatcher and TimerFactory
        var nonDispatcher = BCClientLoader.CreateInstance("NonDispatcher");
        var timerFactoryType = BCClientLoader.GetClientType("TimerFactory`1");
        var taskTimerType = BCClientLoader.GetClientType("TaskTimer");
        var timerFactoryGenericType = timerFactoryType.MakeGenericType(taskTimerType);
        var timerFactory = Activator.CreateInstance(timerFactoryGenericType)!;

        // Create ClientSession
        var sessionType = BCClientLoader.ClientSessionType;
        ClientSession = Activator.CreateInstance(sessionType, jsonClient, nonDispatcher, timerFactory)!;
        Culture = culture;

        OpenSession();
    }

    private static string EnsureClientServicesPath(string url)
    {
        if (url.Contains("/cs/") || url.Contains("/cs?"))
        {
            return url;
        }

        if (url.Contains("?"))
        {
            var queryIndex = url.LastIndexOf('?');
            var pathPart = url.Substring(0, queryIndex).TrimEnd('/');
            var queryPart = url.Substring(queryIndex);
            return $"{pathPart}/cs/{queryPart}";
        }

        return url.TrimEnd('/') + "/cs/";
    }

    protected void OpenSession()
    {
        // Create ClientSessionParameters
        dynamic csParams = BCClientLoader.CreateInstance("ClientSessionParameters");
        csParams.CultureId = Culture;
        csParams.UICultureId = Culture;
        csParams.AdditionalSettings.Add("IncludeControlIdentifier", true);

        // Subscribe to events using reflection (required for late binding)
        // Cast to object to avoid dynamic dispatch which doesn't allow method groups
        object session = ClientSession;
        SubscribeToEvent(session, "MessageToShow", OnMessageToShow);
        SubscribeToEvent(session, "CommunicationError", OnCommunicationError);
        SubscribeToEvent(session, "UnhandledException", OnUnhandledException);
        SubscribeToEvent(session, "InvalidCredentialsError", OnInvalidCredentialsError);
        SubscribeToEvent(session, "UriToShow", OnUriToShow);
        SubscribeToEvent(session, "DialogToShow", OnDialogToShow);

        ClientSession.OpenSessionAsync(csParams);
        AwaitState(_stateReady!);
    }

    public virtual void CloseSession()
    {
        if (ClientSession != null)
        {
            var state = (int)ClientSession.State;
            var ready = (int)_stateReady!;
            var busy = (int)_stateBusy!;
            var inError = (int)_stateInError!;
            var timedOut = (int)_stateTimedOut!;

            if ((state & (ready | busy | inError | timedOut)) != 0)
            {
                CloseAllForms();
                OpenedForm = null;
                OpenedFormName = "";
                ClientSession.CloseSessionAsync();
            }
        }
    }

    public void SetIgnoreServerErrors(bool ignoreServerErrors)
    {
        IgnoreErrors = ignoreServerErrors;
    }

    protected void AwaitState(object state)
    {
        while (!ClientSession.State.Equals(state))
        {
            Thread.Sleep(100);

            var currentState = ClientSession.State;
            string exceptionMessage = "";

            if (currentState.Equals(_stateInError))
                exceptionMessage = "ClientSession in Error state";
            else if (currentState.Equals(_stateTimedOut))
                exceptionMessage = "ClientSession timed out";
            else if (currentState.Equals(_stateUninitialized))
                exceptionMessage = "ClientSession is Uninitialized";

            if (!string.IsNullOrEmpty(exceptionMessage))
            {
                var lastExceptionDetails = GetLastExceptionDetails();
                throw new Exception($"{exceptionMessage}. Last exception: {lastExceptionDetails}");
            }
        }
    }

    private string GetLastExceptionDetails()
    {
        try
        {
            if (HasProperty(ClientSession, "LastException") && ClientSession.LastException != null)
            {
                return ClientSession.LastException.ToString();
            }
        }
        catch
        {
            // Ignore - LastException may not be available in all BC versions
        }
        return "";
    }

    public dynamic OpenForm(int page)
    {
        var currentForm = OpenedForm;
        var currentFormName = currentForm?.Name as string;

        if (currentForm == null || string.IsNullOrEmpty(currentFormName) || currentFormName != OpenedFormName)
        {
            if (currentForm != null && currentFormName != OpenedFormName)
            {
                CloseForm(currentForm);
            }

            var interaction = BCClientLoader.CreateInteraction("OpenFormInteraction");
            interaction.Page = page.ToString();
            OpenedForm = InvokeInteractionAndCatchForm(interaction);

            if (OpenedForm == null)
            {
                throw new Exception($"Form for page {page} not found");
            }

            OpenedFormName = OpenedForm.Name;
        }
        return OpenedForm!;
    }

    public void CloseOpenedForm()
    {
        if (OpenedForm != null)
        {
            var interaction = BCClientLoader.CreateInteraction("CloseFormInteraction", OpenedForm);
            InvokeInteraction(interaction);
            OpenedForm = null;
            OpenedFormName = "";
        }
    }

    public void CloseForm(dynamic? form)
    {
        if (form == null) return;

        var interaction = BCClientLoader.CreateInteraction("CloseFormInteraction", form);
        InvokeInteraction(interaction);

        var currentForm = OpenedForm;
        if (currentForm != null)
        {
            var formName = form.Name as string;
            var currentFormNameValue = currentForm.Name as string;
            if (formName == currentFormNameValue)
            {
                OpenedForm = null;
                OpenedFormName = "";
            }
        }
    }

    public dynamic[] GetAllForms()
    {
        var forms = new List<dynamic>();
        foreach (var form in ClientSession.OpenedForms)
        {
            forms.Add(form);
        }
        return forms.ToArray();
    }

    public void InvokeInteraction(dynamic interaction)
    {
        ClientSession.InvokeInteractionAsync(interaction);
        AwaitState(_stateReady!);
    }

    public dynamic? InvokeInteractionAndCatchForm(dynamic interaction)
    {
        _caughtForm = null;
        object session = ClientSession;
        var handler = SubscribeToEvent(session, "FormToShow", OnFormToShow);

        try
        {
            InvokeInteraction(interaction);

            if (_caughtForm == null)
            {
                CloseAllWarningForms();
            }
        }
        finally
        {
            UnsubscribeFromEvent(session, "FormToShow", handler);
        }

        var form = _caughtForm;
        _caughtForm = null;
        return form;
    }

    public void CloseAllForms()
    {
        foreach (var form in GetAllForms())
        {
            CloseForm(form);
        }
        OpenedForm = null;
        OpenedFormName = "";
    }

    public void CloseAllErrorForms()
    {
        foreach (var form in GetAllForms())
        {
            if (form.ControlIdentifier == "00000000-0000-0000-0800-0000836bd2d2")
            {
                CloseForm(form);
            }
        }
    }

    public void CloseAllWarningForms()
    {
        foreach (var form in GetAllForms())
        {
            if (form.ControlIdentifier == "00000000-0000-0000-0300-0000836bd2d2")
            {
                CloseForm(form);
            }
        }
    }

    public dynamic GetControlByName(dynamic control, string name)
    {
        foreach (var c in control.ContainedControls)
        {
            if (c.Name == name) return c;
        }
        throw new Exception($"Control '{name}' not found");
    }

    public static dynamic? GetControlByCaption(dynamic control, string caption)
    {
        foreach (var c in control.ContainedControls)
        {
            var controlCaption = (string?)c.Caption;
            if (controlCaption?.Replace("&", "") == caption)
                return c;
        }
        return null;
    }

    public void SaveValue(dynamic control, string newValue)
    {
        var interaction = BCClientLoader.CreateInteraction("SaveValueInteraction", control, newValue);
        InvokeInteraction(interaction);
    }

    public dynamic GetActionByName(dynamic control, string name)
    {
        foreach (var c in control.ContainedControls)
        {
            var typeName = c.GetType().Name;
            if (typeName == "ClientActionControl" && c.Name == name)
                return c;
        }
        throw new Exception($"Action '{name}' not found");
    }

    public void InvokeAction(dynamic action)
    {
        var interaction = BCClientLoader.CreateInteraction("InvokeActionInteraction", action);
        InvokeInteraction(interaction);
    }

    public string GetErrorFromErrorForm()
    {
        foreach (var form in ClientSession.OpenedForms)
        {
            if (form.ControlIdentifier == "00000000-0000-0000-0800-0000836bd2d2")
            {
                foreach (var c in form.ContainedControls)
                {
                    if (c.GetType().Name == "ClientStaticStringControl")
                    {
                        return c.StringValue;
                    }
                }
            }
        }
        return "";
    }

    // Event handlers
    private void OnFormToShow(object? sender, dynamic e)
    {
        _caughtForm = e.FormToShow;
    }

    private void OnDialogToShow(object? sender, dynamic e)
    {
        var form = e.DialogToShow;
        if (form.ControlIdentifier == "00000000-0000-0000-0800-0000836bd2d2")
        {
            foreach (var c in form.ContainedControls)
            {
                if (c.GetType().Name == "ClientStaticStringControl")
                {
                    HandleError($"ERROR: {c.StringValue}");
                    break;
                }
            }
        }
        else if (form.ControlIdentifier == "00000000-0000-0000-0300-0000836bd2d2")
        {
            foreach (var c in form.ContainedControls)
            {
                if (c.GetType().Name == "ClientStaticStringControl")
                {
                    Console.Error.WriteLine($"WARNING: {c.StringValue}");
                    break;
                }
            }
        }
    }

    private void OnUriToShow(object? sender, dynamic e)
    {
        // Ignore URI events
    }

    private void OnInvalidCredentialsError(object? sender, dynamic e)
    {
        HandleError("Invalid credentials");
    }

    private void OnUnhandledException(object? sender, dynamic e)
    {
        HandleError($"Unhandled exception: {e.Exception}");
    }

    private void OnCommunicationError(object? sender, dynamic e)
    {
        HandleError($"Communication error: {e.Exception}");
    }

    private void OnMessageToShow(object? sender, dynamic e)
    {
        Console.Error.WriteLine($"BC Message: {e.Message}");
    }

    private void HandleError(string errorMsg)
    {
        Console.Error.WriteLine($"ERROR: {errorMsg}");
        if (!IgnoreErrors)
        {
            throw new Exception(errorMsg);
        }
    }

    private static bool HasProperty(object obj, string propertyName)
    {
        return obj?.GetType().GetProperty(propertyName) != null;
    }

    /// <summary>
    /// Creates a JsonHttpClient instance using reflection to handle different constructor signatures
    /// across BC versions. This allows the code to work with both older (3-param) and newer (4+ param)
    /// versions of the BC client DLL.
    /// </summary>
    private static object CreateJsonHttpClient(Type jsonClientType, Uri addressUri, ICredentials credential, object authScheme)
    {
        // Get all public constructors
        var constructors = jsonClientType.GetConstructors();

        // Find constructors that start with (Uri, ICredentials, AuthenticationScheme, ...)
        foreach (var ctor in constructors.OrderByDescending(c => c.GetParameters().Length))
        {
            var parameters = ctor.GetParameters();
            if (parameters.Length < 3) continue;

            // Check if first 3 params match our expected types
            if (!parameters[0].ParameterType.IsAssignableFrom(typeof(Uri))) continue;
            if (!parameters[1].ParameterType.IsAssignableFrom(typeof(ICredentials))) continue;
            if (!parameters[2].ParameterType.IsEnum) continue; // AuthenticationScheme is an enum

            // Build arguments array with defaults for additional parameters
            var args = new object?[parameters.Length];
            args[0] = addressUri;
            args[1] = credential;
            args[2] = authScheme;

            // Fill in sensible defaults for any additional parameters
            for (int i = 3; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var paramName = parameters[i].Name?.ToLowerInvariant() ?? "";

                // Known parameters with their preferred defaults
                if (paramType == typeof(bool))
                {
                    // antiSSRFDisabled, etc. - default to true for client tools
                    args[i] = true;
                }
                else if (paramType == typeof(string))
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : "";
                }
                else if (paramType == typeof(int))
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : 0;
                }
                else if (paramType.IsClass)
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                }
                else
                {
                    // For other types, try to use default value or type default
                    args[i] = parameters[i].HasDefaultValue
                        ? parameters[i].DefaultValue
                        : (paramType.IsValueType ? Activator.CreateInstance(paramType) : null);
                }
            }

            try
            {
                return ctor.Invoke(args) ?? throw new InvalidOperationException("Constructor returned null");
            }
            catch (TargetInvocationException ex)
            {
                // If this constructor fails, try the next one
                Console.Error.WriteLine($"Constructor with {parameters.Length} params failed: {ex.InnerException?.Message}");
                continue;
            }
        }

        throw new InvalidOperationException(
            $"No compatible JsonHttpClient constructor found. Available constructors: " +
            string.Join(", ", constructors.Select(c =>
                $"({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})")));
    }

    /// <summary>
    /// Subscribes to an event on a dynamic object using reflection.
    /// Returns the delegate that was subscribed so it can be unsubscribed later.
    /// </summary>
    private static Delegate SubscribeToEvent(object target, string eventName, Action<object?, dynamic> handler)
    {
        var targetType = target.GetType();
        var eventInfo = targetType.GetEvent(eventName)
            ?? throw new InvalidOperationException($"Event {eventName} not found on type {targetType.Name}");

        // Create a delegate of the correct type for this event
        var eventHandlerType = eventInfo.EventHandlerType!;
        var invokeMethod = eventHandlerType.GetMethod("Invoke")!;
        var eventArgsType = invokeMethod.GetParameters()[1].ParameterType;

        // Create a method that matches the event signature and invokes our handler
        var methodInfo = typeof(ClientContext).GetMethod(nameof(CreateEventHandler), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(eventArgsType);

        var typedDelegate = (Delegate)methodInfo.Invoke(null, new object[] { handler })!;
        eventInfo.AddEventHandler(target, typedDelegate);

        return typedDelegate;
    }

    /// <summary>
    /// Unsubscribes from an event using the delegate returned from SubscribeToEvent.
    /// </summary>
    private static void UnsubscribeFromEvent(object target, string eventName, Delegate handler)
    {
        var eventInfo = target.GetType().GetEvent(eventName);
        eventInfo?.RemoveEventHandler(target, handler);
    }

    /// <summary>
    /// Creates a typed event handler delegate that forwards to a dynamic handler.
    /// </summary>
    private static EventHandler<T> CreateEventHandler<T>(Action<object?, dynamic> handler)
    {
        return (sender, args) => handler(sender, args!);
    }

    public void Dispose()
    {
        try
        {
            CloseSession();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to close session: {e.Message}");
        }
    }
}
