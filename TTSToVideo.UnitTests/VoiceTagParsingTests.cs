using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TTSToVideo.UnitTests
{
    [TestClass]
    public class VoiceTagParsingTests
    {
        [TestMethod]
        public void ParsePromptIntoStatements_UsesVoiceOnlyForEnclosedText_AndDefaultForNonEnclosed()
        {
            var sut = CreateSutForParsing();

            var prompt = @"<p>
<v n=""Person1"">Paragraph 1</v>

<ip>Prompt image block 1</ip>
<vp>Prompt video block 1</vp>

Paragraph 2
</p>

<v n=""Person3"">Paragraph 3</v>";

            var statements = InvokeParsePromptIntoStatements(sut, prompt, "global");

            Assert.AreEqual(3, statements.Count);

            Assert.AreEqual("Paragraph 1", GetStringProperty(statements[0], "Prompt")?.Trim());
            Assert.AreEqual("Person1", GetStringProperty(statements[0], "VoiceId"));
            Assert.AreEqual("Prompt image block 1", GetStringProperty(statements[0], "ImagePrompt"));
            Assert.AreEqual("Prompt video block 1", GetStringProperty(statements[0], "VideoPrompt"));

            Assert.AreEqual("Paragraph 2", GetStringProperty(statements[1], "Prompt")?.Trim());
            Assert.IsNull(GetStringProperty(statements[1], "VoiceId"));
            Assert.AreEqual("Prompt image block 1", GetStringProperty(statements[1], "ImagePrompt"));
            Assert.AreEqual("Prompt video block 1", GetStringProperty(statements[1], "VideoPrompt"));

            Assert.AreEqual("Paragraph 3", GetStringProperty(statements[2], "Prompt")?.Trim());
            Assert.AreEqual("Person3", GetStringProperty(statements[2], "VoiceId"));
            Assert.IsNull(GetStringProperty(statements[2], "ImagePrompt"));
            Assert.IsNull(GetStringProperty(statements[2], "VideoPrompt"));
        }

        [TestMethod]
        public void ParsePromptIntoStatements_TextWithoutVoiceTag_LeavesVoiceIdNull()
        {
            var sut = CreateSutForParsing();

            var prompt = @"Paragraph A

Paragraph B";

            var statements = InvokeParsePromptIntoStatements(sut, prompt, "global");

            Assert.AreEqual(2, statements.Count);
            Assert.IsTrue(statements.All(s => GetStringProperty(s, "VoiceId") == null));
        }

        private static object CreateSutForParsing()
        {
            var businessType = GetBusinessType();
            return RuntimeHelpers.GetUninitializedObject(businessType);
        }

        private static List<object> InvokeParsePromptIntoStatements(object sut, string prompt, string globalPrompt)
        {
            var method = GetBusinessType().GetMethod("ParsePromptIntoStatements", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "ParsePromptIntoStatements method was not found.");

            var result = method!.Invoke(sut, new object[] { prompt, globalPrompt });
            Assert.IsNotNull(result, "ParsePromptIntoStatements returned null.");

            return ((IEnumerable)result!).Cast<object>().ToList();
        }

        private static Type GetBusinessType()
        {
            var businessType = Type.GetType("TTSToVideo.Business.Implementations.TTSToVideoBusiness, TTSToVideo.Business", throwOnError: false);
            Assert.IsNotNull(businessType, "Unable to load TTSToVideo.Business assembly/type.");
            return businessType!;
        }

        private static string? GetStringProperty(object instance, string propertyName)
        {
            return instance.GetType().GetProperty(propertyName)?.GetValue(instance) as string;
        }
    }
}
