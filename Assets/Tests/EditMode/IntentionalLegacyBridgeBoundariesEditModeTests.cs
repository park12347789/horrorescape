using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using NUnit.Framework;

public sealed class IntentionalLegacyBridgeBoundariesEditModeTests
{
    [Test]
    public void RuntimeCode_AvoidsActiveSceneLookup()
    {
        AssertNoRuntimeScriptCall(@"SceneManager\s*\.\s*GetActiveScene\s*\(", "SceneManager.GetActiveScene()");
    }

    [Test]
    public void RemainingRunSessionSingletonFallbacks_AreLimitedToCoreOwnershipBridges()
    {
        AssertNoRuntimeScriptCall(@"RRunSessionController\s*\.\s*Instance\b", "RRunSessionController.Instance");
    }

    [Test]
    public void RuntimeCode_AvoidsNoiseSingletonFallback()
    {
        AssertNoRuntimeScriptCall(@"NoiseSystem\s*\.\s*Instance\b", "NoiseSystem.Instance");
    }

    [Test]
    public void RuntimeCode_AvoidsPrototypeAudioSingletonDirectAccess()
    {
        AssertNoRuntimeScriptCall(@"PrototypeAudioManager\s*\.\s*Instance\b", "PrototypeAudioManager.Instance");
    }

    [Test]
    public void RuntimeCode_AvoidsBroadGlobalObjectSearches()
    {
        AssertNoRuntimeScriptCall(@"\bFindFirstObjectByType\s*(<|\()", "FindFirstObjectByType");
        AssertNoRuntimeScriptCall(@"\bFindObjectsByType\s*(<|\()", "FindObjectsByType");
        AssertNoRuntimeScriptCall(@"\bFindObjectOfType\s*(<|\()", "FindObjectOfType");
        AssertNoRuntimeScriptCall(@"GameObject\s*\.\s*Find\s*\(", "GameObject.Find");
    }

    [Test]
    public void RuntimeCode_AvoidsGlobalCameraAndTagSearches()
    {
        AssertNoRuntimeScriptCall(@"Camera\s*\.\s*main\b", "Camera.main");
        AssertNoRuntimeScriptCall(@"\bFindGameObjectWithTag\s*\(", "FindGameObjectWithTag");
        AssertNoRuntimeScriptCall(@"\bFindWithTag\s*\(", "FindWithTag");
    }

    [Test]
    public void RemainingCompatibilitySingletonAccessors_AreMarkedObsolete()
    {
        AssertInstancePropertyIsObsolete("RRunSessionController", "RRunSessionResolver");
        AssertInstancePropertyIsObsolete("PrototypeAudioManager", "TryGetCachedInstance");
        AssertInstancePropertyIsObsolete("NoiseSystem", "INoiseEventBus");
    }

    private static void AssertNoRuntimeScriptCall(string expression, string label)
    {
        Regex forbiddenCall = new(expression, RegexOptions.CultureInvariant);
        string[] files = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories);

        for (int index = 0; index < files.Length; index++)
        {
            string path = Normalize(files[index]);

            if (path.Contains("/Editor/"))
            {
                continue;
            }

            string source = StripCommentsAndStrings(File.ReadAllText(files[index]));
            Assert.That(forbiddenCall.IsMatch(source), Is.False, $"{path} contains forbidden runtime call '{label}'.");
        }
    }

    private static string StripCommentsAndStrings(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        char[] scrubbed = source.ToCharArray();
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inChar = false;

        for (int index = 0; index < scrubbed.Length; index++)
        {
            char current = scrubbed[index];
            char next = index + 1 < scrubbed.Length ? scrubbed[index + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\r' || current == '\n')
                {
                    inLineComment = false;
                }
                else
                {
                    scrubbed[index] = ' ';
                }

                continue;
            }

            if (inBlockComment)
            {
                bool isEnd = current == '*' && next == '/';
                scrubbed[index] = ' ';

                if (isEnd)
                {
                    scrubbed[index + 1] = ' ';
                    index++;
                    inBlockComment = false;
                }

                continue;
            }

            if (inString)
            {
                bool isEnd = current == '"' && (!inVerbatimString || next != '"') && (inVerbatimString || !IsEscaped(source, index));
                bool isEscapedQuote = inVerbatimString && current == '"' && next == '"';
                scrubbed[index] = ' ';

                if (isEscapedQuote)
                {
                    scrubbed[index + 1] = ' ';
                    index++;
                    continue;
                }

                if (isEnd)
                {
                    inString = false;
                    inVerbatimString = false;
                }

                continue;
            }

            if (inChar)
            {
                scrubbed[index] = ' ';

                if (current == '\'' && !IsEscaped(source, index))
                {
                    inChar = false;
                }

                continue;
            }

            if (current == '/' && next == '/')
            {
                scrubbed[index] = ' ';
                scrubbed[index + 1] = ' ';
                index++;
                inLineComment = true;
                continue;
            }

            if (current == '/' && next == '*')
            {
                scrubbed[index] = ' ';
                scrubbed[index + 1] = ' ';
                index++;
                inBlockComment = true;
                continue;
            }

            if (current == '"' || (current == '@' && next == '"'))
            {
                inString = true;
                inVerbatimString = current == '@';
                scrubbed[index] = ' ';

                if (inVerbatimString)
                {
                    scrubbed[index + 1] = ' ';
                    index++;
                }

                continue;
            }

            if (current == '\'')
            {
                scrubbed[index] = ' ';
                inChar = true;
            }
        }

        return new string(scrubbed);
    }

    private static bool IsEscaped(string source, int quoteIndex)
    {
        int slashCount = 0;

        for (int index = quoteIndex - 1; index >= 0 && source[index] == '\\'; index--)
        {
            slashCount++;
        }

        return slashCount % 2 == 1;
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    private static void AssertInstancePropertyIsObsolete(string typeName, string expectedMessageFragment)
    {
        Type type = FindTypeByName(typeName);
        Assert.That(type, Is.Not.Null, $"{typeName} type is missing.");

        PropertyInfo property = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, $"{typeName}.Instance is missing.");

        ObsoleteAttribute obsolete = property.GetCustomAttribute<ObsoleteAttribute>();
        Assert.That(obsolete, Is.Not.Null, $"{typeName}.Instance should be marked as a legacy bridge.");
        Assert.That(obsolete.IsError, Is.False);
        Assert.That(obsolete.Message, Does.Contain("Legacy compatibility bridge"));
        Assert.That(obsolete.Message, Does.Contain(expectedMessageFragment));
    }

    private static Type FindTypeByName(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int index = 0; index < assemblies.Length; index++)
        {
            Type found = assemblies[index].GetType(typeName, false);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
