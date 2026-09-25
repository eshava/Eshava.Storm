using System.Data;
using System.Data.Common;

namespace Eshava.Storm.Tests.Infrastructure
{
	/// <summary>
	/// A connection that executes nothing and keeps the last command, so the parameters Storm created can be inspected.
	/// The commands create real SqlParameter instances, which is what Storm meets in production.
	/// </summary>
	internal sealed class RecordingConnection : DbConnection
	{
		public RecordingCommand LastCommand { get; private set; }

		public object ScalarResult { get; set; }

		public int NonQueryResult { get; set; } = 1;

		public override string ConnectionString { get; set; } = "";
		public override string Database => "recording";
		public override string DataSource => "recording";
		public override string ServerVersion => "1.0";
		public override ConnectionState State => ConnectionState.Open;

		public override void ChangeDatabase(string databaseName) { }
		public override void Close() { }
		public override void Open() { }

		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new System.NotSupportedException();

		protected override DbCommand CreateDbCommand()
		{
			LastCommand = new RecordingCommand(this);

			return LastCommand;
		}
	}
}