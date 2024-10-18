# .NET Project Template

This is my .NET project template, with everything set up the way I like.

## Templates used

| File/Project | Template used |
| --- | --- |
| `.gitignore` | `dotnet new gitignore` |
| `MyCompany.MySolution.sln` | `dotnet new sln` |
| `src/ConsoleApp` | `dotnet new consoleapp --use-program-main` |
| `src/ClassLib` | `dotnet new classlib` |
| `src/ClassLib.Tests` | `dotnet new xunit` |

## Other features

- [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/Central-Package-Management)

### Testing

- [Fluent Assertions](https://fluentassertions.com/)

### Code quality and style

- [StyleCop Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)
- [Microsoft.VisualStudio.Threading.Analyzers](https://github.com/microsoft/vs-threading/blob/main/doc/analyzers/index.md)
- [SonarAnalyzer.CSharp](https://github.com/SonarSource/sonar-dotnet)

## License

[`LICENSE`](./LICENSE) is from GitHub, and describes the license for this template repository.
You may replace it with your own license when using this template.
