using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JKToolKit.CodexSDK.Demo.Commands.Common.Pizza;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.Demo.Commands.AgentFrameworkFunctionCalling;

internal sealed class OrderPizzaFunctions
{
    private static readonly JsonSerializerOptions FunctionJsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly InMemoryPizzaService _pizzaService;

    public OrderPizzaFunctions(InMemoryPizzaService pizzaService)
    {
        _pizzaService = pizzaService;
    }

    public IReadOnlyList<AIFunction> CreateFunctions()
    {
        return
        [
            CreateFunction(nameof(GetPizzaMenu), "get_pizza_menu", "Returns the available pizza menu."),
            CreateFunction(nameof(AddPizzaToCart), "add_pizza_to_cart", "Add a pizza to the user's cart; returns the new item and updated cart."),
            CreateFunction(nameof(RemovePizzaFromCart), "remove_pizza_from_cart", "Remove a pizza from the cart by cart item id."),
            CreateFunction(nameof(GetPizzaFromCart), "get_pizza_from_cart", "Returns the specific details of a pizza in the user's cart."),
            CreateFunction(nameof(GetCart), "get_cart", "Returns the user's current cart, including total price and items."),
            CreateFunction(nameof(Checkout), "checkout", "Checks out the user's cart and returns the order status.")
        ];
    }

    public IReadOnlyList<PizzaMenuItem> GetPizzaMenu()
    {
        return _pizzaService.GetMenu();
    }

    public CartDelta AddPizzaToCart(
        [Description("The pizza size.")] PizzaSize size,
        [Description("The toppings to include on the pizza.")] IReadOnlyList<PizzaTopping> toppings,
        [Description("How many pizzas to order.")] int quantity = 1,
        [Description("Special instructions for the kitchen.")] string specialInstructions = "")
    {
        return _pizzaService.AddPizzaToCart(size, toppings, quantity, specialInstructions);
    }

    public RemovePizzaResponse RemovePizzaFromCart([Description("The cart item id to remove.")] int pizzaId)
    {
        return _pizzaService.RemovePizzaFromCart(pizzaId);
    }

    public CartItem? GetPizzaFromCart([Description("The cart item id to inspect.")] int pizzaId)
    {
        return _pizzaService.GetPizzaFromCart(pizzaId);
    }

    public Cart GetCart()
    {
        return _pizzaService.GetCart();
    }

    public CheckoutResponse Checkout()
    {
        return _pizzaService.Checkout();
    }

    private AIFunction CreateFunction(string methodName, string name, string description)
    {
        var method = typeof(OrderPizzaFunctions).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public) ?? throw new MissingMethodException(nameof(OrderPizzaFunctions), methodName);

        return AIFunctionFactory.Create(method, this, name, description, FunctionJsonOptions);
    }
}
