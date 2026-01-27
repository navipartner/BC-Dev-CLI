using System.Text.Json.Serialization;
using BCDev.BC;
using BCDev.Formatters;
using BCDev.Models;
using BCDev.Services;

namespace BCDev;

/// <summary>
/// Source-generated JSON serializer context for AOT compatibility
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LaunchConfigurations))]
[JsonSerializable(typeof(LaunchConfiguration))]
[JsonSerializable(typeof(AppJson))]
[JsonSerializable(typeof(IdRange))]
[JsonSerializable(typeof(AppDependency))]
[JsonSerializable(typeof(CompilerService.CompileResult))]
[JsonSerializable(typeof(CompilerService.CompilerError))]
[JsonSerializable(typeof(PublishService.PublishResult))]
[JsonSerializable(typeof(TestService.TestRunResult))]
[JsonSerializable(typeof(TestService.TestMethodResultDto))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(SuccessResponse))]
[JsonSerializable(typeof(VersionInfo))]
[JsonSerializable(typeof(List<VersionInfo>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(TestRunnerResult))]
[JsonSerializable(typeof(TestMethodResultData))]
[JsonSerializable(typeof(List<TestRunnerResult>))]
[JsonSerializable(typeof(List<TestMethodResultData>))]
[JsonSerializable(typeof(SymbolsResult))]
[JsonSerializable(typeof(SymbolFailure))]
[JsonSerializable(typeof(List<SymbolFailure>))]
internal partial class JsonContext : JsonSerializerContext
{
}
