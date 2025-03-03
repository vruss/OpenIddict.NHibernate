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
    public class OpenIddictAuthorizationMapping<TAuthorization, TApplication, TToken, TKey> : ClassMapping<TAuthorization>
        where TAuthorization : OpenIddictAuthorization<TKey, TApplication, TToken>
        where TApplication : OpenIddictApplication<TKey, TAuthorization, TToken>
        where TToken : OpenIddictToken<TKey, TApplication, TAuthorization>
        where TKey : IEquatable<TKey>
    {
        public OpenIddictAuthorizationMapping()
        {
            this.Id(authorization => authorization.Id, map =>
            {
                map.Generator(Generators.Identity);
            });

            this.Version(authorization => authorization.Version, map =>
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

            this.Property(authorization => authorization.Status, map =>
            {
                map.NotNullable(true);
            });

            this.Property(authorization => authorization.Type, map =>
            {
                map.NotNullable(true);
            });

            this.ManyToOne(authorization => authorization.Application, map =>
            {
                map.ForeignKey("ApplicationId");
            });

            this.Bag(authorization => authorization.Tokens,
                map =>
                {
                    map.Key(key => key.Column("AuthorizationId"));
                },
                map =>
                {
                    map.OneToMany();
                });

            this.Table("OpenIddictAuthorizations");
        }
    }
}
