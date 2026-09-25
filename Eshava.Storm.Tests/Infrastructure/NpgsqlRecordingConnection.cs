namespace Eshava.Storm.Tests.Infrastructure
{
	/// <summary>
	/// A recording connection whose type name makes Storm choose the PostgreSQL command engine
	/// </summary>
	internal sealed class NpgsqlRecordingConnection : RecordingConnection
	{
	}
}