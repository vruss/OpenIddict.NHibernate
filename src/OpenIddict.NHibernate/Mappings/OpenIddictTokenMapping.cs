using System;
using System.ComponentModel;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate.Mappings
{
	/// <summary>
	/// Defines a relational mapping for the Token entity.
	/// </summary>
	/// <typeparam name="TToken">The type of the Token entity.</typeparam>
	/// <typeparam name="TApplication">The type of the Application entity.</typeparam>
	/// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
	/// <typeparam name="TKey">The type of the Key entity.</typeparam>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class OpenIddictTokenMapping<TToken, TApplication, TAuthorization, TKey> : ClassMapping<TToken>
		where TToken : OpenIddictToken<TKey, TApplication, TAuthorization>
		where TApplication : OpenIddictApplication<TKey, TAuthorization, TToken>
		where TAuthorization : OpenIddictAuthorization<TKey, TApplication, TToken>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictTokenMapping()
		{
			this.Id(token => token.Id, map =>
			{
				map.Generator(Generators.Identity);
			});

			this.Version(token => token.Version, map =>
			{
				map.Insert(true);
			});

			this.Property(token => token.CreationDate);

			this.Property(token => token.ExpirationDate);

			this.Property(token => token.Payload, map =>
			{
				map.Length(10000);
			});

			this.Property(token => token.Properties, map =>
			{
				map.Length(10000);
			});

			this.Property(token => token.ReferenceId);

			this.Property(token => token.Status, map =>
			{
				map.NotNullable(true);
			});

			this.Property(token => token.Type, map =>
			{
				map.NotNullable(true);
			});

			this.ManyToOne(token => token.Application, map =>
			{
				map.Column("ApplicationId");
			});

			this.ManyToOne(token => token.Authorization, map =>
			{
				map.Column("AuthorizationId");
			});

			this.Table("OpenIddictTokens");
		}
	}
}
