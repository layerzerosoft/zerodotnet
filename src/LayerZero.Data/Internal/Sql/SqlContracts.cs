using LayerZero.Data.Internal.Translation;

namespace LayerZero.Data.Internal.Sql;

internal enum DataReadMode
{
    List = 0,
    First = 1,
    FirstOrDefault = 2,
    Single = 3,
    SingleOrDefault = 4,
}

internal sealed record DataCommandParameterDescriptor(
    string Name,
    Type ValueType);

internal sealed record CompiledDataCommandTemplate(
    string CommandText,
    IReadOnlyList<DataCommandParameterDescriptor> Parameters,
    IReadOnlyList<string> ResultAliases);

internal interface IDataSqlDialect
{
    CompiledDataCommandTemplate CompileReader(DataReaderCommandTemplate template, DataReadMode mode);

    CompiledDataCommandTemplate CompileAggregate(DataAggregateCommandTemplate template);

    CompiledDataCommandTemplate CompileInsert(DataInsertCommandTemplate template);

    CompiledDataCommandTemplate CompileUpdate(DataUpdateCommandTemplate template);

    CompiledDataCommandTemplate CompileDelete(DataDeleteCommandTemplate template);

    CompiledDataCommandTemplate CompileRawSql(DataSqlStatement statement);
}
