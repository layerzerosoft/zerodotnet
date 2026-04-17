using System.Linq.Expressions;

namespace LayerZero.Data.Internal.Execution;

internal enum DataAggregateKind
{
    Any = 0,
    Count = 1,
    LongCount = 2,
    Sum = 3,
    Min = 4,
    Max = 5,
}

internal sealed record DataJoinModel(
    Type RightEntityType,
    LambdaExpression LeftKey,
    LambdaExpression RightKey);

internal sealed record DataOrderingModel(
    LambdaExpression Selector,
    bool Descending);

internal sealed record DataQueryModel(
    Type RootEntityType,
    Type RowType,
    IReadOnlyList<DataJoinModel> Joins,
    IReadOnlyList<LambdaExpression> Filters,
    IReadOnlyList<DataOrderingModel> Orderings,
    int? Skip,
    int? Take)
{
    public static DataQueryModel Create<TEntity>()
        where TEntity : notnull =>
        new(typeof(TEntity), typeof(TEntity), [], [], [], null, null);

    public DataQueryModel AddFilter(LambdaExpression predicate) =>
        this with { Filters = Filters.Concat([predicate]).ToArray() };

    public DataQueryModel AddJoin<TRight>(LambdaExpression leftKey, LambdaExpression rightKey)
        where TRight : notnull =>
        this with
        {
            RowType = typeof(DataJoin<,>).MakeGenericType(RowType, typeof(TRight)),
            Joins = Joins.Concat([new DataJoinModel(typeof(TRight), leftKey, rightKey)]).ToArray(),
        };

    public DataQueryModel AddOrdering(LambdaExpression selector, bool descending) =>
        this with { Orderings = Orderings.Concat([new DataOrderingModel(selector, descending)]).ToArray() };

    public DataQueryModel WithSkip(int count) => this with { Skip = count };

    public DataQueryModel WithTake(int count) => this with { Take = count };
}

internal sealed record DataAssignmentModel(
    LambdaExpression Property,
    object? Value,
    Type ValueType);

internal sealed record DataUpdateModel(
    Type EntityType,
    IReadOnlyList<LambdaExpression> Filters,
    IReadOnlyList<DataAssignmentModel> Assignments)
{
    public static DataUpdateModel Create<TEntity>()
        where TEntity : notnull =>
        new(typeof(TEntity), [], []);

    public DataUpdateModel AddFilter(LambdaExpression predicate) =>
        this with { Filters = Filters.Concat([predicate]).ToArray() };

    public DataUpdateModel AddAssignment(LambdaExpression property, object? value, Type valueType) =>
        this with { Assignments = Assignments.Concat([new DataAssignmentModel(property, value, valueType)]).ToArray() };
}

internal sealed record DataDeleteModel(
    Type EntityType,
    IReadOnlyList<LambdaExpression> Filters)
{
    public static DataDeleteModel Create<TEntity>()
        where TEntity : notnull =>
        new(typeof(TEntity), []);

    public DataDeleteModel AddFilter(LambdaExpression predicate) =>
        this with { Filters = Filters.Concat([predicate]).ToArray() };
}

internal interface IDataContextSession
{
    ValueTask<IReadOnlyList<TResult>> ListAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken);

    ValueTask<TResult> FirstAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken);

    ValueTask<TResult?> FirstOrDefaultAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken);

    ValueTask<TResult> SingleAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken);

    ValueTask<TResult?> SingleOrDefaultAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken);

    ValueTask<TResult> AggregateAsync<TRow, TResult>(
        DataQueryModel model,
        DataAggregateKind aggregate,
        LambdaExpression? selector,
        CancellationToken cancellationToken);

    ValueTask InsertAsync<TEntity>(TEntity entity, CancellationToken cancellationToken)
        where TEntity : notnull;

    ValueTask<int> ExecuteUpdateAsync<TEntity>(DataUpdateModel model, CancellationToken cancellationToken)
        where TEntity : notnull;

    ValueTask<int> ExecuteDeleteAsync<TEntity>(DataDeleteModel model, CancellationToken cancellationToken)
        where TEntity : notnull;
}
