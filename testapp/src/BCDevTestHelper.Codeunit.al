codeunit 50102 "BCDev Test Helper"
{
    procedure MethodThatErrors()
    begin
        Error('This is a deliberate error from MethodThatErrors');
    end;

    procedure Level1()
    begin
        Level2();
    end;

    procedure Level2()
    begin
        Level3();
    end;

    procedure Level3()
    begin
        Error('Error at Level3 - this should show a nested call stack');
    end;
}
