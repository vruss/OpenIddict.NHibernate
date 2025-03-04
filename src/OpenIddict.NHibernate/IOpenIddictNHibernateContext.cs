using System.Threading;
using System.Threading.Tasks;
using NHibernate;

namespace OpenIddict.NHibernate
{
	/// <summary>
	/// Exposes the NHibernate session used by the OpenIddict stores.
	/// </summary>
	public interface IOpenIddictNHibernateContext
	{
		/// <summary>
		/// Gets the <see cref="ISession"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the
		/// asynchronous operation, whose result returns the NHibernate session.
		/// </returns>
		ValueTask<ISession> GetSessionAsync(CancellationToken cancellationToken);
	}
}
