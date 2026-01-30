codeunit 50200 "Broken Code"
{
    [Obsolete('Use NewMethod instead', '2024-01-01')]
    procedure OldMethod(): Text
    begin
        exit('This method is obsolete');
    end;

    procedure BrokenMethod(): Text
    var
        UndefinedVar: Text;
    begin
        // This will generate an error - calling undefined procedure
        UndefinedVar := NonExistentProcedure();

        // This will generate a warning (AL0432) - calling obsolete method
        UndefinedVar += OldMethod();

        exit(UndefinedVar);
    end;
}
