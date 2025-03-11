using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace OpenIddict.NHibernate.Models
{
	/// <summary>
	/// Represents an OpenIddict authorization.
	/// </summary>
	public class OpenIddictNHibernateAuthorization : OpenIddictNHibernateAuthorization<string, OpenIddictNHibernateApplication, OpenIddictNHibernateToken>
	{
		public OpenIddictNHibernateAuthorization()
		{
			// Generate a new string identifier.
			this.Id = Guid.NewGuid().ToString();
		}
	}

	/// <summary>
	/// Represents an OpenIddict authorization.
	/// </summary>
	public class OpenIddictNHibernateAuthorization<TKey> : OpenIddictNHibernateAuthorization<TKey, OpenIddictNHibernateApplication<TKey>, OpenIddictNHibernateToken<TKey>>
		where TKey : IEquatable<TKey>
	{
	}

	/// <summary>
	/// Represents an OpenIddict authorization.
	/// </summary>
	[DebuggerDisplay("Id = {Id.ToString(),nq} ; Subject = {Subject,nq} ; Type = {Type,nq} ; Status = {Status,nq}")]
	public class OpenIddictNHibernateAuthorization<TKey, TApplication, TToken>
		where TKey : notnull, IEquatable<TKey>
		where TApplication : class
		where TToken : class
	{
		/// <summary>
		/// Gets or sets the application associated with the current authorization.
		/// </summary>
		public virtual TApplication? Application { get; set; }

		/// <summary>
		/// Gets or sets the concurrency token.
		/// </summary>
		public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

		/// <summary>
		/// Gets or sets the UTC creation date of the current authorization.
		/// </summary>
		public virtual DateTime? CreationDate { get; set; }

		/// <summary>
		/// Gets or sets the unique identifier associated with the current authorization.
		/// </summary>
		public virtual TKey? Id { get; set; }

		/// <summary>
		/// Gets or sets the additional properties serialized as a JSON object,
		/// or <see langword="null"/> if no bag was associated with the current authorization.
		/// </summary>
		[StringSyntax(StringSyntaxAttribute.Json)]
		public virtual string? Properties { get; set; }

		/// <summary>
		/// Gets or sets the scopes associated with the current
		/// authorization, serialized as a JSON array.
		/// </summary>
		[StringSyntax(StringSyntaxAttribute.Json)]
		public virtual string? Scopes { get; set; }

		/// <summary>
		/// Gets or sets the status of the current authorization.
		/// </summary>
		public virtual string? Status { get; set; }

		/// <summary>
		/// Gets or sets the subject associated with the current authorization.
		/// </summary>
		public virtual string? Subject { get; set; }

		/// <summary>
		/// Gets the list of tokens associated with the current authorization.
		/// </summary>
		public virtual ICollection<TToken> Tokens { get; set; } = new HashSet<TToken>();

		/// <summary>
		/// Gets or sets the type of the current authorization.
		/// </summary>
		public virtual string? Type { get; set; }
	}
}
