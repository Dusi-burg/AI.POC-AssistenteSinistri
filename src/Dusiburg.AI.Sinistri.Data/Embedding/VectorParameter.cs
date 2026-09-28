using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;

namespace Dusiburg.AI.Sinistri.Data.Embedding;

/// <summary>
/// Parametro <c>VECTOR</c> nativo (<see cref="SqlVector{T}"/>, Microsoft.Data.SqlClient ≥ 6.1) per Dapper, che il tipo non lo
/// conosce (fase-4.md §3, opzione preferita): niente serializzazione JSON né <c>CAST</c> nel testo SQL.
/// </summary>
internal sealed class VectorParameter(float[] values) : SqlMapper.ICustomQueryParameter
{
    public void AddParameter(IDbCommand command, string name)
    {
        var parameter = new SqlParameter(name, new SqlVector<float>(values));
        command.Parameters.Add(parameter);
    }
}
