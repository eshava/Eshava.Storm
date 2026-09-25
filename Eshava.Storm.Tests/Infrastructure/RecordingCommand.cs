using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Eshava.Storm.Tests.Infrastructure
{
	internal sealed class RecordingCommand : DbCommand
	{
		private readonly RecordingConnection _connection;
		private readonly SqlCommand _parameterHolder = new SqlCommand();

		public RecordingCommand(RecordingConnection connection)
		{
			_connection = connection;
		}

		public SqlParameterCollection SqlParameters => _parameterHolder.Parameters;

		public override string CommandText { get; set; }
		public override int CommandTimeout { get; set; }
		public override CommandType CommandType { get; set; }
		public override bool DesignTimeVisible { get; set; }
		public override UpdateRowSource UpdatedRowSource { get; set; }
		protected override DbConnection DbConnection { get; set; }
		protected override DbParameterCollection DbParameterCollection => _parameterHolder.Parameters;
		protected override DbTransaction DbTransaction { get; set; }

		public override void Cancel() { }
		public override void Prepare() { }
		public override int ExecuteNonQuery() => _connection.NonQueryResult;
		public override object ExecuteScalar() => _connection.ScalarResult;

		protected override DbParameter CreateDbParameter() => new SqlParameter();

		protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => _connection.Reader?.Invoke() ?? throw new System.NotSupportedException();
	}
}