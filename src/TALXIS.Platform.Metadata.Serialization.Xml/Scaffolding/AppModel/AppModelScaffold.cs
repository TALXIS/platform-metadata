using System.Text.RegularExpressions;
using System.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-app-model template post-action scripts:
/// stamps generated short ids into the rendered sitemap placeholders and
/// registers the app in Solution.xml (AppModule type 80, then SiteMap type 62,
/// the old script order). The Customizations.xml AppModules/AppModuleSiteMaps
/// node patches are deliberately dropped (they appended duplicates on every run;
/// the TALXIS SDK owns those nodes at build time).
/// </summary>
public static class AppModelScaffold
{
    public static ScaffoldResult Apply(AppModelScaffoldRequest request)
    {
        var result = new ScaffoldResult();

        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.AppModule,
            SchemaName = request.AppSchemaName,
            Behavior = 0,
        });

        var siteMapPath = ManagedXmlFileLocator.Locate(request.SiteMapFilePath);
        if (siteMapPath != null)
        {
            var doc = new XmlDocument();
            doc.Load(siteMapPath);
            var text = doc.OuterXml;
            text = Regex.Replace(text, Regex.Escape("areaidexample"), SiteMapIdGenerator.NewShortId());
            text = Regex.Replace(text, Regex.Escape("groupidexample"), SiteMapIdGenerator.NewShortId());
            text = Regex.Replace(text, Regex.Escape("subareaidexample"), SiteMapIdGenerator.NewShortId());
            doc.LoadXml(text);
            doc.Save(siteMapPath);
        }
        else
        {
            result.AddWarning($"No AppModuleSiteMap XML found at '{request.SiteMapFilePath}' - skipping id generation.");
        }

        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.SiteMap,
            SchemaName = request.AppSchemaName,
            Behavior = 0,
        });

        return result;
    }
}
