using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class FormXmlMergeTests
{
    private const string BaseFormXml = @"
<forms>
  <systemform>
    <form>
      <tabs>
        <tab id=""{00000000-0000-0000-0000-000000000001}"" name=""general"" showlabel=""true"">
          <columns>
            <column width=""100%"">
              <sections>
                <section id=""{00000000-0000-0000-0000-000000000010}"" name=""section1"" showlabel=""true"">
                  <rows>
                    <row>
                      <cell id=""{00000000-0000-0000-0000-000000000100}"">
                        <control id=""name"" classid=""{4273EDBD-AC1D-40D3-9FB2-095C621B552D}"" datafieldname=""fullname"" />
                      </cell>
                    </row>
                  </rows>
                </section>
                <section id=""{00000000-0000-0000-0000-000000000011}"" name=""section2"" showlabel=""true"">
                  <rows>
                    <row>
                      <cell id=""{00000000-0000-0000-0000-000000000101}"">
                        <control id=""email"" classid=""{4273EDBD-AC1D-40D3-9FB2-095C621B552D}"" datafieldname=""emailaddress1"" />
                      </cell>
                    </row>
                  </rows>
                </section>
              </sections>
            </column>
          </columns>
        </tab>
      </tabs>
    </form>
  </systemform>
</forms>";

    [Fact]
    public void Merge_AddedTab()
    {
        var baseForm = XDocument.Parse(BaseFormXml);
        var layer = XDocument.Parse(@"
<forms>
  <systemform>
    <form>
      <tabs>
        <tab id=""{00000000-0000-0000-0000-000000000002}"" name=""details"" solutionaction=""Added"">
          <columns>
            <column width=""100%"">
              <sections>
                <section id=""{00000000-0000-0000-0000-000000000020}"" name=""detailsSection"">
                  <rows>
                    <row>
                      <cell id=""{00000000-0000-0000-0000-000000000200}"">
                        <control id=""phone"" classid=""{4273EDBD-AC1D-40D3-9FB2-095C621B552D}"" datafieldname=""telephone1"" />
                      </cell>
                    </row>
                  </rows>
                </section>
              </sections>
            </column>
          </columns>
        </tab>
      </tabs>
    </form>
  </systemform>
</forms>");

        var result = FormXmlMerge.Merge(baseForm, layer);

        var tabs = result.Descendants("tab").ToList();
        Assert.Equal(2, tabs.Count);
        Assert.Equal("{00000000-0000-0000-0000-000000000001}", tabs[0].Attribute("id")?.Value);
        Assert.Equal("{00000000-0000-0000-0000-000000000002}", tabs[1].Attribute("id")?.Value);
        // solutionaction should be stripped from the merged result
        Assert.Null(tabs[1].Attribute("solutionaction"));
    }

    [Fact]
    public void Merge_RemovedSection()
    {
        var baseForm = XDocument.Parse(BaseFormXml);
        var layer = XDocument.Parse(@"
<forms>
  <systemform>
    <form>
      <tabs>
        <tab id=""{00000000-0000-0000-0000-000000000001}"">
          <columns>
            <column width=""100%"">
              <sections>
                <section id=""{00000000-0000-0000-0000-000000000011}"" name=""section2"" solutionaction=""Removed"" />
              </sections>
            </column>
          </columns>
        </tab>
      </tabs>
    </form>
  </systemform>
</forms>");

        var result = FormXmlMerge.Merge(baseForm, layer);

        var sections = result.Descendants("section").ToList();
        Assert.Single(sections);
        Assert.Equal("{00000000-0000-0000-0000-000000000010}", sections[0].Attribute("id")?.Value);
    }

    [Fact]
    public void Merge_ModifiedControl()
    {
        var baseForm = XDocument.Parse(BaseFormXml);
        var layer = XDocument.Parse(@"
<forms>
  <systemform>
    <form>
      <tabs>
        <tab id=""{00000000-0000-0000-0000-000000000001}"">
          <columns>
            <column width=""100%"">
              <sections>
                <section id=""{00000000-0000-0000-0000-000000000010}"">
                  <rows>
                    <row>
                      <cell id=""{00000000-0000-0000-0000-000000000100}"">
                        <control id=""name"" classid=""{4273EDBD-AC1D-40D3-9FB2-095C621B552D}"" datafieldname=""fullname"" disabled=""true"" solutionaction=""Modified"" />
                      </cell>
                    </row>
                  </rows>
                </section>
              </sections>
            </column>
          </columns>
        </tab>
      </tabs>
    </form>
  </systemform>
</forms>");

        var result = FormXmlMerge.Merge(baseForm, layer);

        var control = result.Descendants("control").First(c => c.Attribute("id")?.Value == "name");
        Assert.Equal("true", control.Attribute("disabled")?.Value);
        Assert.Equal("{4273EDBD-AC1D-40D3-9FB2-095C621B552D}", control.Attribute("classid")?.Value);
        Assert.Null(control.Attribute("solutionaction"));
    }

    [Fact]
    public void Merge_AddedCell()
    {
        var baseForm = XDocument.Parse(BaseFormXml);
        var layer = XDocument.Parse(@"
<forms>
  <systemform>
    <form>
      <tabs>
        <tab id=""{00000000-0000-0000-0000-000000000001}"">
          <columns>
            <column width=""100%"">
              <sections>
                <section id=""{00000000-0000-0000-0000-000000000010}"">
                  <rows>
                    <row solutionaction=""Added"">
                      <cell id=""{00000000-0000-0000-0000-000000000102}"">
                        <control id=""jobtitle"" classid=""{4273EDBD-AC1D-40D3-9FB2-095C621B552D}"" datafieldname=""jobtitle"" />
                      </cell>
                    </row>
                  </rows>
                </section>
              </sections>
            </column>
          </columns>
        </tab>
      </tabs>
    </form>
  </systemform>
</forms>");

        var result = FormXmlMerge.Merge(baseForm, layer);

        var section = result.Descendants("section")
            .First(s => s.Attribute("id")?.Value == "{00000000-0000-0000-0000-000000000010}");
        var rows = section.Descendants("row").ToList();
        Assert.Equal(2, rows.Count);
        var newCell = rows[1].Descendants("control").FirstOrDefault();
        Assert.NotNull(newCell);
        Assert.Equal("jobtitle", newCell!.Attribute("datafieldname")?.Value);
    }

    [Fact]
    public void Merge_PreservesUnchanged()
    {
        var baseForm = XDocument.Parse(BaseFormXml);
        // Layer with only structural wrappers, no solutionaction anywhere
        var layer = XDocument.Parse(@"
<forms>
  <systemform>
    <form>
      <tabs>
      </tabs>
    </form>
  </systemform>
</forms>");

        var result = FormXmlMerge.Merge(baseForm, layer);

        // All original content should be preserved
        var tabs = result.Descendants("tab").ToList();
        Assert.Single(tabs);
        var sections = result.Descendants("section").ToList();
        Assert.Equal(2, sections.Count);
        var controls = result.Descendants("control").ToList();
        Assert.Equal(2, controls.Count);
    }

    [Fact]
    public void ComputeDiff_DetectsAddedElement()
    {
        var baseForm = XDocument.Parse(BaseFormXml);
        var modifiedXml = BaseFormXml.Replace(
            "</tabs>",
            @"<tab id=""{00000000-0000-0000-0000-000000000002}"" name=""newtab"">
                <columns><column width=""100%""><sections /></column></columns>
              </tab>
            </tabs>");
        var modifiedForm = XDocument.Parse(modifiedXml);

        var diff = FormXmlMerge.ComputeDiff(baseForm, modifiedForm);

        var addedTab = diff.Descendants("tab")
            .FirstOrDefault(t => t.Attribute("solutionaction")?.Value == "Added");
        Assert.NotNull(addedTab);
        Assert.Equal("{00000000-0000-0000-0000-000000000002}", addedTab!.Attribute("id")?.Value);
    }

    [Fact]
    public void ComputeDiff_DetectsRemovedElement()
    {
        var baseForm = XDocument.Parse(BaseFormXml);
        // Remove section2 from the modified form
        var modifiedForm = XDocument.Parse(BaseFormXml);
        modifiedForm.Descendants("section")
            .First(s => s.Attribute("id")?.Value == "{00000000-0000-0000-0000-000000000011}")
            .Remove();

        var diff = FormXmlMerge.ComputeDiff(baseForm, modifiedForm);

        var removedSection = diff.Descendants("section")
            .FirstOrDefault(s => s.Attribute("solutionaction")?.Value == "Removed");
        Assert.NotNull(removedSection);
        Assert.Equal("{00000000-0000-0000-0000-000000000011}", removedSection!.Attribute("id")?.Value);
    }

    [Fact]
    public void ComputeDiff_IdenticalForms()
    {
        var baseForm = XDocument.Parse(BaseFormXml);
        var modifiedForm = XDocument.Parse(BaseFormXml);

        var diff = FormXmlMerge.ComputeDiff(baseForm, modifiedForm);

        // No elements should have solutionaction
        var actions = diff.Descendants()
            .Where(e => e.Attribute("solutionaction") != null)
            .ToList();
        Assert.Empty(actions);
    }

    [Fact]
    public void Merge_Roundtrip_DiffThenMerge()
    {
        var baseForm = XDocument.Parse(BaseFormXml);
        var modifiedXml = BaseFormXml.Replace(
            "</tabs>",
            @"<tab id=""{00000000-0000-0000-0000-000000000002}"" name=""newtab"">
                <columns><column width=""100%""><sections /></column></columns>
              </tab>
            </tabs>");
        var modifiedForm = XDocument.Parse(modifiedXml);

        var diff = FormXmlMerge.ComputeDiff(baseForm, modifiedForm);
        var result = FormXmlMerge.Merge(baseForm, diff);

        // The result should have the new tab
        var tabs = result.Descendants("tab").ToList();
        Assert.Equal(2, tabs.Count);
    }
}
