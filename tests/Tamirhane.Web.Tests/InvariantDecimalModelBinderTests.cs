using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Tamirhane.Web.ModelBinding;

namespace Tamirhane.Web.Tests;

/// <summary>Ondalık alanlar sayfanın kültürü Türkçe olsa da noktalı ve virgüllü yazımı aynı okur. Docker gerekmez.</summary>
public sealed class InvariantDecimalModelBinderTests
{
    [Theory]
    [InlineData("10.5", 10.5)]
    [InlineData("10,5", 10.5)]
    [InlineData(" 20 ", 20)]
    [InlineData("-1", -1)]
    [InlineData("20.125", 20.125)] // iki ondalık kuralı domain'de; burada sadece okunur
    public async Task Dot_and_comma_are_both_decimal_separators(string text, double expected)
    {
        var (context, _) = await BindAsync(text, typeof(decimal));

        Assert.True(context.Result.IsModelSet);
        Assert.Equal((decimal)expected, context.Result.Model);
    }

    [Theory]
    [InlineData("1.500,5")] // binlik ayırıcı: "1.500" mü "1,5" mi belli değil
    [InlineData("abc")]
    [InlineData("1e3")]
    public async Task Ambiguous_or_non_numeric_text_is_an_error(string text)
    {
        var (context, state) = await BindAsync(text, typeof(decimal));

        Assert.False(context.Result.IsModelSet);
        Assert.Equal(ModelValidationState.Invalid, state.GetFieldValidationState("Rate"));
    }

    [Fact]
    public async Task Empty_value_is_an_error_only_for_required_numbers()
    {
        var (_, required) = await BindAsync("", typeof(decimal));
        var (_, optional) = await BindAsync("", typeof(decimal?));

        Assert.Equal(ModelValidationState.Invalid, required.GetFieldValidationState("Rate"));
        Assert.NotEqual(ModelValidationState.Invalid, optional.GetFieldValidationState("Rate"));
    }

    private static async Task<(DefaultModelBindingContext Context, ModelStateDictionary State)> BindAsync(string text, Type type)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); // sayfanın kültürü Türkçe olsa bile
        try
        {
            var state = new ModelStateDictionary();
            var context = new DefaultModelBindingContext
            {
                ModelName = "Rate",
                ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(type),
                ModelState = state,
                ValueProvider = new QueryStringValueProvider(BindingSource.Form,
                    new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["Rate"] = text }),
                    CultureInfo.InvariantCulture),
            };
            await new InvariantDecimalModelBinder().BindModelAsync(context);
            return (context, state);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
