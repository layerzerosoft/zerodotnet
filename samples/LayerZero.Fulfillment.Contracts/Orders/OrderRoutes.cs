namespace LayerZero.Fulfillment.Contracts.Orders;

public static class OrderRoutes
{
    public const string Collection = "/orders";
    public const string Resource = "/orders/{id:guid}";
    public const string Cancel = "/orders/{id:guid}/cancel";
    public const string Timeline = "/orders/{id:guid}/timeline";
    public const string DeadLetters = "/deadletters";
    public const string RequeueDeadLetter = "/deadletters/{messageId}";
}
