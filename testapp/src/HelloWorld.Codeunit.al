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
}
