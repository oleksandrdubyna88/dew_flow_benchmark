// Shared fixtures of the pre-registration tests: the two registered files the consistency check reads (the SDK a C# prompt names against the pin of stacks.json).
/** The two registered files the guard reads for its consistency check; every other registered file is a placeholder text. */
export const stacksText = sdk => `${JSON.stringify({ pins: { claude: '2.1.310', dotnetSdk: sdk } })}\n`;
export const promptsText = sdk => `${JSON.stringify({ prompts: { csharp: { v1: { text: `Implement it using C#. Use C# 14 on .NET 10 (SDK ${sdk} is installed).` }, v2: { text: `Modify it. Use C# 14 on .NET 10 (SDK ${sdk} is installed).` } }, node: { v1: { text: 'Implement it using TypeScript.' }, v2: { text: 'Modify it.' } } } })}\n`;
