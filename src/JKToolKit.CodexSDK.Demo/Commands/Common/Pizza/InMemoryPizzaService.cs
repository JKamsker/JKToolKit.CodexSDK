namespace JKToolKit.CodexSDK.Demo.Commands.Common.Pizza;

internal sealed class InMemoryPizzaService
{
    private static readonly IReadOnlyList<PizzaMenuItem> Menu =
    [
        new("Margherita", PizzaSize.Small, 9.50m, [PizzaTopping.Cheese]),
        new("Margherita", PizzaSize.Medium, 12.00m, [PizzaTopping.Cheese]),
        new("Margherita", PizzaSize.Large, 15.00m, [PizzaTopping.Cheese]),
        new("House Special", PizzaSize.Large, 18.50m, [PizzaTopping.Cheese, PizzaTopping.Pepperoni, PizzaTopping.Mushrooms])
    ];

    private readonly List<CartItem> _items = [];
    private int _nextItemId = 1;

    public IReadOnlyList<PizzaMenuItem> GetMenu() => Menu;

    public CartDelta AddPizzaToCart(
        PizzaSize size,
        IReadOnlyList<PizzaTopping> toppings,
        int quantity,
        string specialInstructions)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be greater than zero.");
        }

        var item = new CartItem(
            _nextItemId++,
            "Custom Pizza",
            size,
            toppings,
            quantity,
            specialInstructions,
            GetUnitPrice(size, toppings));

        _items.Add(item);
        return new CartDelta(item, GetCart());
    }

    public RemovePizzaResponse RemovePizzaFromCart(int pizzaId)
    {
        var removed = _items.RemoveAll(item => item.Id == pizzaId) > 0;
        return new RemovePizzaResponse(removed, GetCart());
    }

    public CartItem? GetPizzaFromCart(int pizzaId)
    {
        return _items.FirstOrDefault(item => item.Id == pizzaId);
    }

    public Cart GetCart()
    {
        var total = _items.Sum(item => item.UnitPrice * item.Quantity);
        return new Cart(_items.ToArray(), total);
    }

    public CheckoutResponse Checkout()
    {
        if (_items.Count == 0)
        {
            return new CheckoutResponse("none", "Cart is empty.", GetCart());
        }

        return new CheckoutResponse($"PIZZA-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", "Paid and queued for the kitchen.", GetCart());
    }

    private static decimal GetUnitPrice(PizzaSize size, IReadOnlyCollection<PizzaTopping> toppings)
    {
        var basePrice = size switch
        {
            PizzaSize.Small => 9.00m,
            PizzaSize.Medium => 12.00m,
            PizzaSize.Large => 15.00m,
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown pizza size.")
        };

        return basePrice + Math.Max(0, toppings.Count - 1) * 1.25m;
    }
}
