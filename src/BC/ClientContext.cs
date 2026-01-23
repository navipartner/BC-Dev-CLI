using System.Net;
using System.Reflection;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Framework.UI.Client.Interactions;

namespace BCDev.BC;

/// <summary>
/// Client context for connecting to Business Central
/// </summary>
public class ClientContext : IDisposable
{
    protected ClientSession ClientSession { get; private set; } = null!;
    protected string Culture { get; private set; } = "en-US";
    internal ClientLogicalForm? OpenedForm { get; private set; }
    protected string OpenedFormName { get; private set; } = "";
    private ClientLogicalForm? _caughtForm;
    protected bool IgnoreErrors { get; private set; } = true;

    public string SessionId
    {
        get
        {
            if (ClientSession?.Info == null)
                return "";
            return ClientSession.Info.SessionId;
        }
    }

    public ClientContext(string serviceUrl, AuthenticationScheme authenticationScheme, ICredentials credential,
        TimeSpan interactionTimeout, string culture = "en-US")
    {
        Initialize(serviceUrl, authenticationScheme, credential, interactionTimeout, culture);
    }

    public ClientContext(string serviceUrl, string authenticationScheme, ICredentials credential,
        TimeSpan interactionTimeout, string culture = "en-US")
    {
        var auth = (AuthenticationScheme)Enum.Parse(typeof(AuthenticationScheme), authenticationScheme);
        Initialize(serviceUrl, auth, credential, interactionTimeout, culture);
    }

    private void Initialize(string serviceUrl, AuthenticationScheme authenticationScheme, ICredentials credential,
        TimeSpan interactionTimeout, string culture)
    {
        // Configure keep-alive for long-running operations
        ServicePointManager.SetTcpKeepAlive(true,
            (int)TimeSpan.FromMinutes(120).TotalMilliseconds,
            (int)TimeSpan.FromSeconds(10).TotalMilliseconds);

        // Disable SSL verification for dev environments
        SslVerification.Disable();

        var clientServicesUrl = EnsureClientServicesPath(serviceUrl);
        var addressUri = new Uri(clientServicesUrl);

        var jsonClient = new JsonHttpClient(addressUri, credential, authenticationScheme)
            ?? throw new Exception("Failed to create JsonHttpClient");

        // Set timeout on the HTTP client via reflection
        var httpClientField = typeof(JsonHttpClient).GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new Exception("Could not find httpClient field");
        var httpClient = httpClientField.GetValue(jsonClient) as HttpClient
            ?? throw new Exception("Could not get httpClient instance");
        httpClient.Timeout = interactionTimeout;

        ClientSession = new ClientSession(jsonClient, new NonDispatcher(), new TimerFactory<TaskTimer>());
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
            // Insert /cs/ before the query string
            var queryIndex = url.LastIndexOf('?');
            var pathPart = url.Substring(0, queryIndex).TrimEnd('/');
            var queryPart = url.Substring(queryIndex);
            return $"{pathPart}/cs/{queryPart}";
        }

        return url.TrimEnd('/') + "/cs/";
    }

    protected void OpenSession()
    {
        var csParams = new ClientSessionParameters
        {
            CultureId = Culture,
            UICultureId = Culture
        };
        csParams.AdditionalSettings.Add("IncludeControlIdentifier", true);

        ClientSession.MessageToShow += OnMessageToShow;
        ClientSession.CommunicationError += OnCommunicationError;
        ClientSession.UnhandledException += OnUnhandledException;
        ClientSession.InvalidCredentialsError += OnInvalidCredentialsError;
        ClientSession.UriToShow += OnUriToShow;
        ClientSession.DialogToShow += OnDialogToShow;

        ClientSession.OpenSessionAsync(csParams);
        AwaitState(ClientSessionState.Ready);
    }

    public virtual void CloseSession()
    {
        if (ClientSession != null)
        {
            if (ClientSession.State.HasFlag(ClientSessionState.Ready | ClientSessionState.Busy |
                                            ClientSessionState.InError | ClientSessionState.TimedOut))
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

    protected void AwaitState(ClientSessionState state)
    {
        while (ClientSession.State != state)
        {
            Thread.Sleep(100);

            var exceptionMessage = ClientSession.State switch
            {
                ClientSessionState.InError => "ClientSession in Error state",
                ClientSessionState.TimedOut => "ClientSession timed out",
                ClientSessionState.Uninitialized => "ClientSession is Uninitialized",
                _ => ""
            };

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
            dynamic session = ClientSession;
            if (HasProperty(session, "LastException") && session.LastException != null)
            {
                return session.LastException.ToString();
            }
        }
        catch
        {
            // Ignore - LastException may not be available in all BC versions
        }
        return "";
    }

    public ClientLogicalForm OpenForm(int page)
    {
        if (OpenedForm == null || string.IsNullOrEmpty(OpenedForm.Name) || OpenedForm.Name != OpenedFormName)
        {
            if (OpenedForm != null && OpenedForm.Name != OpenedFormName)
            {
                CloseForm(OpenedForm);
            }

            var interaction = new OpenFormInteraction { Page = page.ToString() };
            OpenedForm = InvokeInteractionAndCatchForm(interaction);

            if (OpenedForm == null)
            {
                throw new Exception($"Form for page {page} not found");
            }

            OpenedFormName = OpenedForm.Name;
        }
        return OpenedForm;
    }

    public void CloseOpenedForm()
    {
        if (OpenedForm != null)
        {
            InvokeInteraction(new CloseFormInteraction(OpenedForm));
            OpenedForm = null;
            OpenedFormName = "";
        }
    }

    public void CloseForm(ClientLogicalForm? form)
    {
        if (form == null) return;

        InvokeInteraction(new CloseFormInteraction(form));

        if (OpenedForm != null && form.Name == OpenedForm.Name)
        {
            OpenedForm = null;
            OpenedFormName = "";
        }
    }

    public ClientLogicalForm[] GetAllForms()
    {
        return ClientSession.OpenedForms.ToArray();
    }

    public void InvokeInteraction(ClientInteraction interaction)
    {
        ClientSession.InvokeInteractionAsync(interaction);
        AwaitState(ClientSessionState.Ready);
    }

    public ClientLogicalForm? InvokeInteractionAndCatchForm(ClientInteraction interaction)
    {
        _caughtForm = null;
        ClientSession.FormToShow += OnFormToShow;

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
            ClientSession.FormToShow -= OnFormToShow;
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

    public ClientLogicalControl GetControlByName(ClientLogicalControl control, string name)
    {
        return control.ContainedControls.First(c => c.Name == name);
    }

    public static ClientLogicalControl? GetControlByCaption(ClientLogicalControl control, string caption)
    {
        return control.ContainedControls.FirstOrDefault(c => c.Caption?.Replace("&", "") == caption);
    }

    public void SaveValue(ClientLogicalControl control, string newValue)
    {
        InvokeInteraction(new SaveValueInteraction(control, newValue));
    }

    public ClientActionControl GetActionByName(ClientLogicalControl control, string name)
    {
        return (ClientActionControl)control.ContainedControls
            .First(c => c is ClientActionControl && c.Name == name);
    }

    public void InvokeAction(ClientActionControl action)
    {
        InvokeInteraction(new InvokeActionInteraction(action));
    }

    public string GetErrorFromErrorForm()
    {
        foreach (var form in ClientSession.OpenedForms)
        {
            if (form.ControlIdentifier == "00000000-0000-0000-0800-0000836bd2d2")
            {
                var errorControl = form.ContainedControls.FirstOrDefault(c => c is ClientStaticStringControl);
                if (errorControl != null)
                {
                    return ((ClientStaticStringControl)errorControl).StringValue;
                }
            }
        }
        return "";
    }

    // Event handlers
    private void OnFormToShow(object? sender, ClientFormToShowEventArgs e)
    {
        _caughtForm = e.FormToShow;
    }

    private void OnDialogToShow(object? sender, ClientDialogToShowEventArgs e)
    {
        var form = e.DialogToShow;
        if (form.ControlIdentifier == "00000000-0000-0000-0800-0000836bd2d2")
        {
            var errorControl = form.ContainedControls.FirstOrDefault(c => c is ClientStaticStringControl);
            if (errorControl != null)
            {
                HandleError($"ERROR: {((ClientStaticStringControl)errorControl).StringValue}");
            }
        }
        else if (form.ControlIdentifier == "00000000-0000-0000-0300-0000836bd2d2")
        {
            var warningControl = form.ContainedControls.FirstOrDefault(c => c is ClientStaticStringControl);
            if (warningControl != null)
            {
                Console.Error.WriteLine($"WARNING: {((ClientStaticStringControl)warningControl).StringValue}");
            }
        }
    }

    private void OnUriToShow(object? sender, ClientUriToShowEventArgs e)
    {
        // Ignore URI events
    }

    private void OnInvalidCredentialsError(object? sender, MessageToShowEventArgs e)
    {
        HandleError("Invalid credentials");
    }

    private void OnUnhandledException(object? sender, ExceptionEventArgs e)
    {
        HandleError($"Unhandled exception: {e.Exception}");
    }

    private void OnCommunicationError(object? sender, ExceptionEventArgs e)
    {
        HandleError($"Communication error: {e.Exception}");
    }

    private void OnMessageToShow(object? sender, MessageToShowEventArgs e)
    {
        // Log messages to stderr for debugging
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
