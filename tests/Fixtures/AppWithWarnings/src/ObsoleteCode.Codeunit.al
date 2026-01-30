codeunit 50100 "Obsolete Code"
{
    [Obsolete('Use NewMethod instead', '2024-01-01')]
    procedure OldMethod(): Text
    begin
        exit('This method is obsolete');
    end;

    [Obsolete('Use AnotherNewMethod instead', '2024-01-01')]
    procedure AnotherOldMethod(): Text
    begin
        exit('This method is also obsolete');
    end;

    procedure NewMethod(): Text
    begin
        exit('This is the new method');
    end;

    procedure CallerMethod(): Text
    var
        Result: Text;
    begin
        // These calls will generate AL0432 warnings
        Result := OldMethod();
        Result += AnotherOldMethod();
        exit(Result);
    end;
}
