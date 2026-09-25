using System.Data;
using System.Data.Common;

namespace Eshava.Storm.Tests.Infrastructure
{
	/// <summary>
	/// Wraps another connection the way a profiler does, under a type name that does not tell the provider
	/// </summary>
	internal sealed class WrappingConnection : DbConnection
	{
		public WrappingConnection(DbConnection wrappedConnection)
		{
			WrappedConnection = wrappedConnection;
		}

		public DbConnection WrappedConnection { get; }

		public override string ConnectionString
		{
			get => WrappedConnection.ConnectionString;
			set => WrappedConnection.ConnectionString = value;
		}

		public override string Database => WrappedConnection.Database;
		public override string DataSource => WrappedConnection.DataSource;
		public override string ServerVersion => WrappedConnection.ServerVersion;
		public override ConnectionState State => WrappedConnection.State;

		public override void ChangeDatabase(string databaseName) => WrappedConnection.ChangeDatabase(databaseName);
		public override void Close() => WrappedConnection.Close();
		public override void Open() => WrappedConnection.Open();

		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => WrappedConnection.BeginTransaction(isolationLevel);
		protected override DbCommand CreateDbCommand() => WrappedConnection.CreateCommand();
	}
}