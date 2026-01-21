codeunit 50100 "Hello World"
{
    /// <summary>
    /// A simple procedure that returns a greeting message.
    /// </summary>
    procedure GetGreeting(): Text
    begin
        exit('Hello from BCDev Test App!');
    end;

    /// <summary>
    /// Shows a greeting message.
    /// </summary>
    procedure ShowGreeting()
    var
        GreetingMsg: Label 'Hello from BCDev Test App!';
    begin
        Message(GreetingMsg);
    end;

    /// <summary>
    /// Gets the count of companies in the system.
    /// This procedure uses Company table from Base Application to demonstrate dependency.
    /// </summary>
    procedure GetCompanyCount(): Integer
    var
        Company: Record Company;
    begin
        exit(Company.Count());
    end;
}
