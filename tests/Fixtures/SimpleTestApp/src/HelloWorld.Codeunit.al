codeunit 50000 "Hello World"
{
    procedure GetMessage(): Text
    begin
        exit('Hello from integration test!');
    end;
}
