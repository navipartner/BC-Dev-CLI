codeunit 50101 "BCDev Tests"
{
    Subtype = Test;
    TestPermissions = Disabled;

    [Test]
    procedure TestThatPasses()
    begin
        // This test should pass
        if 1 + 1 <> 2 then
            Error('Basic math should work');
    end;

    [Test]
    procedure TestSimpleAssertionFailure()
    begin
        // This test will fail with a simple assertion
        Error('Expected 1 to equal 2 but it does not');
    end;

    [Test]
    procedure TestErrorWithStackTrace()
    var
        Helper: Codeunit "BCDev Test Helper";
    begin
        // This test will fail by calling a helper that errors
        Helper.MethodThatErrors();
    end;

    [Test]
    procedure TestNestedCallStackTrace()
    var
        Helper: Codeunit "BCDev Test Helper";
    begin
        // This test will fail with a deeper call stack
        Helper.Level1();
    end;

    [Test]
    procedure TestRecordNotFound()
    var
        Customer: Record Customer;
    begin
        // This test will fail when trying to get a non-existent record
        Customer.Get('NONEXISTENT-CUSTOMER-NO');
    end;

    [Test]
    procedure TestDivisionByZero()
    var
        x: Integer;
        y: Integer;
    begin
        // This test will fail with a division by zero error
        x := 10;
        y := 0;
        x := x div y;
    end;

    [Test]
    procedure TestAnotherPassingTest()
    var
        Text1: Text;
        Text2: Text;
    begin
        // Another passing test to verify mixed results
        Text1 := 'Hello';
        Text2 := 'Hello';
        if Text1 <> Text2 then
            Error('Strings should match');
    end;

    [Test]
    procedure TestExpectedErrorNotRaised()
    begin
        // This test expects an error but none is raised
        asserterror DoNothing();
        if GetLastErrorText() = '' then
            Error('Expected an error but none occurred');
    end;

    local procedure DoNothing()
    begin
        // Does nothing, so no error is raised
    end;
}
