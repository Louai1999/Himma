using Himma.Api.Domain.Entities;

namespace Himma.Api.Data.Seed;

/// <summary>
/// Common Arabic / Gulf foods with Arabic and English names. Values are per 100 g and are
/// typical averages (home recipes vary), good enough for daily tracking, not for medical use.
/// The Arabic name is the seed key and must never change.
/// </summary>
public static class FoodSeedData
{
    public static IReadOnlyList<Food> All =>
    [
        // Bread and breakfast
        F("خبز عربي أبيض", "White pita bread", 275, 9.1, 55.7, 1.2, 60, "رغيف", "loaf"),
        F("خبز عربي أسمر (بر)", "Whole wheat pita bread", 266, 9.8, 55.0, 2.6, 60, "رغيف", "loaf"),
        F("مناقيش زعتر", "Zaatar manakish", 330, 7.0, 40.0, 16.0, 120, "قطعة", "piece"),
        F("مناقيش جبنة", "Cheese manakish", 300, 12.0, 33.0, 13.0, 120, "قطعة", "piece"),
        F("شوفان", "Oats", 389, 16.9, 66.3, 6.9, 40, "نصف كوب", "half cup"),
        F("بيض مسلوق", "Boiled egg", 155, 12.6, 1.1, 10.6, 50, "بيضة", "egg"),
        F("بيض مقلي", "Fried egg", 196, 13.6, 0.8, 14.8, 46, "بيضة", "egg"),
        F("فول مدمس بالزيت", "Ful medames with oil", 140, 7.0, 16.0, 5.0, 200, "صحن", "plate"),
        F("فلافل", "Falafel", 333, 13.3, 31.8, 17.8, 50, "3 حبات", "3 pieces"),
        F("حمص بالطحينة", "Hummus with tahini", 166, 7.9, 14.3, 9.6, 60, "3 ملاعق كبيرة", "3 tbsp"),
        F("متبل باذنجان", "Baba ghanoush", 120, 2.5, 8.0, 9.0, 60, "3 ملاعق كبيرة", "3 tbsp"),
        F("لبنة", "Labneh", 155, 7.0, 4.0, 12.0, 30, "ملعقتان كبيرتان", "2 tbsp"),
        F("جبنة بيضاء", "White cheese (feta)", 264, 14.2, 4.1, 21.3, 30, "شريحة", "slice"),
        F("عسل", "Honey", 304, 0.3, 82.4, 0.0, 21, "ملعقة كبيرة", "tbsp"),
        F("طحينة", "Tahini", 595, 17.0, 21.2, 53.8, 15, "ملعقة كبيرة", "tbsp"),
        F("زيت زيتون", "Olive oil", 884, 0.0, 0.0, 100.0, 14, "ملعقة كبيرة", "tbsp"),

        // Dairy and drinks
        F("حليب كامل الدسم", "Whole milk", 61, 3.2, 4.8, 3.3, 250, "كوب", "cup"),
        F("حليب قليل الدسم", "Low-fat milk", 42, 3.4, 5.0, 1.0, 250, "كوب", "cup"),
        F("زبادي كامل الدسم", "Whole-milk yogurt", 61, 3.5, 4.7, 3.3, 170, "علبة", "pot"),
        F("زبادي يوناني قليل الدسم", "Low-fat Greek yogurt", 59, 10.2, 3.6, 0.4, 170, "علبة", "pot"),
        F("لبن (شنينة)", "Laban (yogurt drink)", 40, 3.3, 4.6, 1.0, 250, "كوب", "cup"),
        F("قهوة عربية بدون سكر", "Arabic coffee, no sugar", 2, 0.1, 0.3, 0.0, 90, "3 فناجين", "3 small cups"),
        F("شاي بالسكر", "Tea with sugar", 11, 0.0, 2.8, 0.0, 150, "كوب", "cup"),
        F("عصير برتقال طبيعي", "Fresh orange juice", 45, 0.7, 10.4, 0.2, 250, "كوب", "cup"),

        // Main dishes
        F("أرز أبيض مطبوخ", "Cooked white rice", 130, 2.7, 28.0, 0.3, 150, "كوب", "cup"),
        F("أرز بسمتي مطبوخ", "Cooked basmati rice", 121, 3.5, 25.2, 0.4, 150, "كوب", "cup"),
        F("كبسة دجاج", "Chicken kabsa", 165, 9.0, 20.0, 5.5, 300, "صحن", "plate"),
        F("مندي لحم", "Lamb mandi", 190, 10.0, 18.0, 9.0, 300, "صحن", "plate"),
        F("برياني دجاج", "Chicken biryani", 175, 9.0, 22.0, 6.0, 300, "صحن", "plate"),
        F("مقلوبة دجاج", "Chicken maqluba", 160, 9.0, 18.0, 6.0, 300, "صحن", "plate"),
        F("هريس", "Harees", 130, 7.0, 18.0, 3.5, 250, "صحن", "plate"),
        F("جريش", "Jareesh", 120, 4.0, 18.0, 3.5, 250, "صحن", "plate"),
        F("ملوخية بالدجاج", "Molokhia with chicken", 80, 6.0, 5.0, 4.0, 250, "صحن", "plate"),
        F("مطبق لحم", "Meat mutabbaq", 260, 10.0, 25.0, 13.0, 150, "قطعة", "piece"),
        F("شوربة عدس", "Lentil soup", 70, 4.5, 10.0, 1.5, 250, "صحن", "bowl"),
        F("ورق عنب", "Stuffed vine leaves", 160, 3.0, 22.0, 7.0, 100, "6 حبات", "6 pieces"),
        F("كبة مقلية", "Fried kibbeh", 270, 13.0, 24.0, 13.0, 60, "حبة", "piece"),
        F("سمبوسة لحم", "Meat sambousek", 290, 9.0, 25.0, 17.0, 40, "حبة", "piece"),
        F("شاورما دجاج (ساندويتش)", "Chicken shawarma sandwich", 200, 11.0, 20.0, 9.0, 250, "ساندويتش", "sandwich"),
        F("شاورما لحم (ساندويتش)", "Meat shawarma sandwich", 230, 12.0, 20.0, 11.0, 250, "ساندويتش", "sandwich"),

        // Proteins
        F("صدر دجاج مشوي", "Grilled chicken breast", 165, 31.0, 0.0, 3.6, 120, "قطعة", "piece"),
        F("شيش طاووق", "Shish tawook", 160, 25.0, 3.0, 5.5, 150, "سيخ", "skewer"),
        F("كفتة مشوية", "Grilled kofta", 250, 18.0, 4.0, 18.0, 100, "سيخ", "skewer"),
        F("لحم غنم مطبوخ", "Cooked lamb", 294, 25.0, 0.0, 21.0, 120, "حصة", "serving"),
        F("سمك هامور مشوي", "Grilled hammour (grouper)", 118, 24.8, 0.0, 1.3, 150, "حصة", "serving"),
        F("سلمون مشوي", "Grilled salmon", 208, 20.0, 0.0, 13.0, 150, "حصة", "serving"),
        F("تونة معلبة بالماء", "Canned tuna in water", 116, 25.5, 0.0, 0.8, 100, "علبة صغيرة", "small can"),

        // Sides and salads
        F("تبولة", "Tabbouleh", 115, 2.0, 10.0, 8.0, 150, "صحن صغير", "small plate"),
        F("فتوش", "Fattoush", 90, 1.8, 10.0, 5.0, 150, "صحن صغير", "small plate"),
        F("سلطة خضراء بدون صلصة", "Green salad, no dressing", 20, 1.0, 3.5, 0.2, 150, "صحن", "plate"),
        F("بطاطا مقلية", "French fries", 312, 3.4, 41.0, 15.0, 100, "حصة صغيرة", "small serving"),
        F("بطاطا مسلوقة", "Boiled potato", 87, 1.9, 20.0, 0.1, 150, "حبة متوسطة", "medium potato"),
        F("خيار", "Cucumber", 15, 0.7, 3.6, 0.1, 100, "حبة", "piece"),
        F("طماطم", "Tomato", 18, 0.9, 3.9, 0.2, 120, "حبة", "piece"),

        // Fruit and nuts
        F("تمر", "Dates", 282, 2.5, 75.0, 0.4, 24, "3 تمرات", "3 dates"),
        F("موز", "Banana", 89, 1.1, 22.8, 0.3, 120, "حبة متوسطة", "medium banana"),
        F("تفاح", "Apple", 52, 0.3, 13.8, 0.2, 180, "حبة", "piece"),
        F("برتقال", "Orange", 47, 0.9, 11.8, 0.1, 150, "حبة", "piece"),
        F("بطيخ", "Watermelon", 30, 0.6, 7.6, 0.2, 300, "شريحة", "slice"),
        F("لوز", "Almonds", 579, 21.2, 21.6, 49.9, 28, "حفنة", "handful"),
        F("فستق حلبي", "Pistachios", 560, 20.2, 27.2, 45.3, 28, "حفنة", "handful"),

        // Sweets
        F("كنافة", "Kunafa", 340, 6.0, 38.0, 18.0, 100, "قطعة", "piece"),
        F("بقلاوة", "Baklava", 428, 6.7, 37.6, 29.0, 30, "قطعة", "piece"),
        F("لقيمات", "Luqaimat", 350, 5.0, 50.0, 15.0, 60, "5 حبات", "5 pieces"),
    ];

    private static Food F(
        string name, string nameEn, double kcal, double protein, double carbs, double fat,
        double? servingGrams, string? servingLabel, string? servingLabelEn) => new()
    {
        Name = name,
        NameEn = nameEn,
        KcalPer100g = kcal,
        ProteinPer100g = protein,
        CarbsPer100g = carbs,
        FatPer100g = fat,
        ServingGrams = servingGrams,
        ServingLabel = servingLabel,
        ServingLabelEn = servingLabelEn,
    };
}
