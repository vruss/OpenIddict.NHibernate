using System;
using System.ComponentModel;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate.Mappings
{
	/// <summary>
	/// Defines a relational mapping for the Authorization entity.
	/// </summary>
	/// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
	/// <typeparam name="TApplication">The type of the Application entity.</typeparam>
	/// <typeparam name="TToken">The type of the Token entity.</typeparam>
	/// <typeparam name="TKey">The type of the Key entity.</typeparam>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class OpenIddictNHibernateAuthorizationMapping<TAuthorization, TApplication, TToken, TKey> : ClassMapping<TAuthorization>
		where TAuthorization : OpenIddictNHibernateAuthorization<TKey, TApplication, TToken>
		where TApplication : OpenIddictNHibernateApplication<TKey, TAuthorization, TToken>
		where TToken : OpenIddictNHibernateToken<TKey, TApplication, TAuthorization>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictNHibernateAuthorizationMapping()
		{
			this.Id(authorization => authorization.Id, map =>
			{
				map.Generator(Generators.Identity);
			});

			this.Version(authorization => authorization.ConcurrencyToken, map =>
			{
				map.Insert(true);
			});

			this.Property(authorization => authorization.Properties, map =>
			{
				map.Length(10000);
			});

			this.Property(authorization => authorization.Scopes, map =>
			{
				map.Length(10000);
			});

			this.Property(authorization => authorization.Subject);
			this.Property(authorization => authorization.Status);
			this.Property(authorization => authorization.Type);
			this.Property(authorization => authorization.CreationDate);

			this.ManyToOne(authorization => authorization.Application, map =>
			{
				map.ForeignKey("ApplicationId");
			});

			this.Bag(authorization => authorization.Tokens, map =>
			{
				map.Key(key => key.Column("AuthorizationId"));
			}, map =>
			{
				map.OneToMany();
			});

			this.Table("OpenIddictAuthorizations");
		}
	}
}
