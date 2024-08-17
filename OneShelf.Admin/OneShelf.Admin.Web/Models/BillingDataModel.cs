namespace OneShelf.Admin.Web.Models;

public class BillingDataModel
{
    public BillingDataModel(BillingModel model, int? last, string title)
    {
        Model = model;
        Last = last;
        Title = title;
    }

    public BillingModel Model { get; }

    public int? Last { get; }

    public string Title { get; }
}