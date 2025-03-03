using System;
using System.ComponentModel;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate.Mappings
{
    /// <summary>
    /// Defines a relational mapping for the Application entity.
    /// </summary>
    /// <typeparam name="TApplication">The type of the Application entity.</typeparam>
    /// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
    /// <typeparam name="TToken">The type of the Token entity.</typeparam>
    /// <typeparam name="TKey">The type of the Key entity.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class OpenIddictApplicationMapping<TApplication, TAuthorization, TToken, TKey> : ClassMapping<TApplication>
        where TApplication : OpenIddictApplication<TKey, TAuthorization, TToken>
        where TAuthorization : OpenIddictAuthorization<TKey, TApplication, TToken>
        where TToken : OpenIddictToken<TKey, TApplication, TAuthorization>
        where TKey : IEquatable<TKey>
    {
        public OpenIddictApplicationMapping()
        {
            this.Id(application => application.Id, map =>
            {
                map.Generator(Generators.Identity);
            });

            this.Version(application => application.Version, map =>
            {
                map.Insert(true);
            });

            this.Property(application => application.ClientId, map =>
            {
                map.NotNullable(true);
                map.Unique(true);
            });

            this.Property(application => application.ClientSecret);

            this.Property(application => application.ConsentType);

            this.Property(application => application.DisplayName);

            this.Property(application => application.Permissions, map =>
            {
                map.Length(10000);
            });

            this.Property(application => application.PostLogoutRedirectUris, map =>
            {
                map.Length(10000);
            });

            this.Property(application => application.Properties, map =>
            {
                map.Length(10000);
            });

            this.Property(application => application.RedirectUris, map =>
            {
                map.Length(10000);
            });

            this.Property(application => application.Type, map =>
            {
                map.NotNullable(true);
            });

            this.Bag(application => application.Authorizations,
                map =>
                {
                    map.Key(key => key.Column("ApplicationId"));
                },
                map =>
                {
                    map.OneToMany();
                });

            this.Bag(application => application.Tokens,
                map =>
                {
                    map.Key(key => key.Column("ApplicationId"));
                },
                map =>
                {
                    map.OneToMany();
                });

            this.Table("OpenIddictApplications");
        }
    }
}
