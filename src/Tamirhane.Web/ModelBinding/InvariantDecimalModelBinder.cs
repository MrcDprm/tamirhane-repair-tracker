using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Tamirhane.Web.ModelBinding;

/// <summary>
/// Ondalık alanlar (KDV oranı, fiyat) kültürden bağımsız okunur. Tarayıcı type="number" alanı her dilde noktayla
/// gönderir ("10.5"); MVC'nin varsayılanı sayfanın kültürüyle okur ve Türkçede bunu geçersiz sayar ya da 105 yapar.
/// Elle yazılan virgüllü değer de ("10,5") kabul edilir.
/// </summary>
public sealed class InvariantDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None)
            return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);

        var text = value.FirstValue?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            // Boş bırakılan zorunlu alan [Required] ya da varsayılan doğrulamayla yakalanır.
            if (Nullable.GetUnderlyingType(context.ModelType) is null)
                context.ModelState.TryAddModelError(context.ModelName, "Bir sayı yaz.");
            return Task.CompletedTask;
        }

        // Binlik ayırıcı kabul edilmez: "1.500" ile "1,5" karışmasın, sadece tek ondalık ayırıcı.
        if (decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var number))
            context.Result = ModelBindingResult.Success(number);
        else
            context.ModelState.TryAddModelError(context.ModelName, "Geçerli bir sayı yaz (ör. 20 ya da 10,5).");
        return Task.CompletedTask;
    }
}

public sealed class InvariantDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(decimal) ? new InvariantDecimalModelBinder() : null;
}
