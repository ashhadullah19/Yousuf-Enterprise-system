namespace Yousuf_Enterprise_system.Models;

public static class UnitConversion
{
    // KG-equivalent of one unit. Maund is the standard 40kg "man" used in South Asian
    // agri/commodity trade; Ton is metric (1000kg). Bag has no fixed weight, so its factor
    // comes from SystemSetting.BagWeightKg rather than being hardcoded here.
    public static decimal ToKgFactor(UnitOfMeasure unit, decimal bagWeightKg) => unit switch
    {
        UnitOfMeasure.KG => 1m,
        UnitOfMeasure.Ton => 1000m,
        UnitOfMeasure.Maund => 40m,
        UnitOfMeasure.Bag => bagWeightKg,
        _ => 1m
    };

    // Display-only conversion — never used for rate/amount calculations, which stay tied to
    // the quantity and unit actually recorded on the transaction.
    public static decimal Convert(decimal quantity, UnitOfMeasure from, UnitOfMeasure to, decimal bagWeightKg)
    {
        if (from == to)
        {
            return quantity;
        }

        var factor = ToKgFactor(to, bagWeightKg);
        if (factor == 0)
        {
            return 0;
        }

        return quantity * ToKgFactor(from, bagWeightKg) / factor;
    }
}
