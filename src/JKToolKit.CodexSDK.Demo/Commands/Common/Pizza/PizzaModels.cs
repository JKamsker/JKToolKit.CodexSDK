namespace JKToolKit.CodexSDK.Demo.Commands.Common.Pizza;

internal enum PizzaSize
{
    Small,
    Medium,
    Large
}

internal enum PizzaTopping
{
    Cheese,
    Pepperoni,
    Mushrooms,
    Olives,
    Peppers
}

internal sealed record PizzaMenuItem(
    string Name,
    PizzaSize Size,
    decimal Price,
    IReadOnlyList<PizzaTopping> IncludedToppings);

internal sealed record CartItem(
    int Id,
    string Name,
    PizzaSize Size,
    IReadOnlyList<PizzaTopping> Toppings,
    int Quantity,
    string SpecialInstructions,
    decimal UnitPrice);

internal sealed record Cart(IReadOnlyList<CartItem> Items, decimal Total);

internal sealed record CartDelta(CartItem AddedItem, Cart Cart);

internal sealed record RemovePizzaResponse(bool Removed, Cart Cart);

internal sealed record CheckoutResponse(string OrderId, string Status, Cart Cart);
