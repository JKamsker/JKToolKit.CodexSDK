using System.ComponentModel;
using JKToolKit.CodexSDK.Demo.Commands.Common.Pizza;
using Microsoft.SemanticKernel;

namespace JKToolKit.CodexSDK.Demo.Commands.SemanticKernelFunctionCalling.Pizza;

internal sealed class OrderPizzaPlugin
{
    private readonly InMemoryPizzaService _pizzaService;

    public OrderPizzaPlugin(InMemoryPizzaService pizzaService)
    {
        _pizzaService = pizzaService;
    }

    [KernelFunction("get_pizza_menu")]
    [Description("Returns the available pizza menu.")]
    public IReadOnlyList<PizzaMenuItem> GetPizzaMenu()
    {
        return _pizzaService.GetMenu();
    }

    [KernelFunction("add_pizza_to_cart")]
    [Description("Add a pizza to the user's cart; returns the new item and updated cart.")]
    public CartDelta AddPizzaToCart(
        [Description("The pizza size.")] PizzaSize size,
        [Description("The toppings to include on the pizza.")] IReadOnlyList<PizzaTopping> toppings,
        [Description("How many pizzas to order.")] int quantity = 1,
        [Description("Special instructions for the kitchen.")] string specialInstructions = "")
    {
        return _pizzaService.AddPizzaToCart(size, toppings, quantity, specialInstructions);
    }

    [KernelFunction("remove_pizza_from_cart")]
    [Description("Remove a pizza from the cart by cart item id.")]
    public RemovePizzaResponse RemovePizzaFromCart([Description("The cart item id to remove.")] int pizzaId)
    {
        return _pizzaService.RemovePizzaFromCart(pizzaId);
    }

    [KernelFunction("get_pizza_from_cart")]
    [Description("Returns the specific details of a pizza in the user's cart.")]
    public CartItem? GetPizzaFromCart([Description("The cart item id to inspect.")] int pizzaId)
    {
        return _pizzaService.GetPizzaFromCart(pizzaId);
    }

    [KernelFunction("get_cart")]
    [Description("Returns the user's current cart, including total price and items.")]
    public Cart GetCart()
    {
        return _pizzaService.GetCart();
    }

    [KernelFunction("checkout")]
    [Description("Checks out the user's cart and returns the order status.")]
    public CheckoutResponse Checkout()
    {
        return _pizzaService.Checkout();
    }
}
