<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:decimal-format decimal-separator="," grouping-separator=" " />
  <xsl:template match="/">
    <xsl:processing-instruction name="mso-application">
      <xsl:text>progid="Excel.Sheet"</xsl:text>
    </xsl:processing-instruction>

    <Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet"
     xmlns:o="urn:schemas-microsoft-com:office:office"
     xmlns:x="urn:schemas-microsoft-com:office:excel"
     xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet"
     xmlns:html="http://www.w3.org/TR/REC-html40"
     xmlns:r="http://www.itrf.ru/xslt">
      <DocumentProperties xmlns="urn:schemas-microsoft-com:office:office">
        <Version>12.00</Version>
      </DocumentProperties>
      <Styles>
        <Style ss:ID="Default" ss:Name="Normal">
          <Alignment ss:Vertical="Bottom"/>
          <Borders/>
          <Font ss:FontName="Arial" x:CharSet="204" x:Family="Swiss" ss:Color="#000000" ss:Size="9"/>
          <Interior/>
          <NumberFormat/>
          <Protection/>
        </Style>
        <Style ss:ID="s81">
          <Alignment ss:Horizontal="Left" ss:Vertical="Bottom"/>
          <Borders>
            <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="2"/>
          </Borders>
          <Font ss:FontName="Arial" x:CharSet="204" x:Family="Swiss" ss:Size="16"
           ss:Color="#000000" ss:Bold="1"/>
        </Style>
        <Style ss:ID="s84">
          <Alignment ss:Horizontal="Left" ss:Vertical="Bottom"/>
          <Borders/>
          <Font ss:FontName="Arial" x:CharSet="204" x:Family="Swiss" ss:Size="16"
           ss:Color="#000000" ss:Bold="1"/>
        </Style>
        <Style ss:ID="s89">
          <Alignment ss:Horizontal="Right" ss:Vertical="Bottom"/>
          <Font ss:FontName="Arial" x:CharSet="204" x:Family="Swiss" ss:Size="8"
           ss:Color="#000000"/>
          <NumberFormat ss:Format="General Date"/>
        </Style>
        <Style ss:ID="s90">
          <Alignment ss:Horizontal="Right" ss:Vertical="Bottom"/>
          <Font ss:FontName="Arial" x:CharSet="204" x:Family="Swiss" ss:Size="8"
           ss:Color="#000000"/>
        </Style>
        <Style ss:ID="s93">
          <Borders>
            <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Left" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Right" ss:LineStyle="Continuous" ss:Weight="1"/>
          </Borders>
        </Style>
        <Style ss:ID="s93Int">
          <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
          <Borders>
            <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Left" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Right" ss:LineStyle="Continuous" ss:Weight="1"/>
          </Borders>
          <NumberFormat />
        </Style>
        <Style ss:ID="s93Numeric">
          <Borders>
            <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Left" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Right" ss:LineStyle="Continuous" ss:Weight="1"/>
          </Borders>
          <NumberFormat ss:Format="Standard"/>
        </Style>
        <Style ss:ID="s97">
          <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
          <Borders>
            <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="2"/>
            <Border ss:Position="Left" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Right" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Top" ss:LineStyle="Continuous" ss:Weight="2"/>
          </Borders>
          <Font ss:FontName="Arial" x:CharSet="204" x:Family="Swiss" ss:Color="#000000"
           ss:Bold="1"/>
        </Style>
        <Style ss:ID="s100">
          <Alignment ss:Horizontal="Center" ss:Vertical="Bottom"/>
          <Borders>
            <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Left" ss:LineStyle="Continuous" ss:Weight="1"/>
            <Border ss:Position="Right" ss:LineStyle="Continuous" ss:Weight="1"/>
          </Borders>
          <NumberFormat ss:Format="Short Date"/>
        </Style>
        <Style ss:ID="s102">
          <Borders>
            <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1"/>
          </Borders>
        </Style>
        <Style ss:ID="s106">
          <Alignment ss:Horizontal="Center" ss:Vertical="Top"/>
          <Font ss:FontName="Arial" x:CharSet="204" x:Family="Swiss" ss:Size="7"
           ss:Color="#000000"/>
        </Style>
        <Style ss:ID="s177">
          <Alignment ss:Horizontal="Left" ss:Vertical="Bottom"/>
          <Borders>
            <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="2"/>
          </Borders>
          <Font ss:FontName="Arial" x:CharSet="204" x:Family="Swiss" ss:Size="10"
           ss:Color="#000000" ss:Bold="1"/>
          <Interior ss:Color="#F2F2F2" ss:Pattern="Solid"/>
        </Style>
      </Styles>
      <Worksheet ss:Name="Отчет">
        <Table>
          <xsl:for-each select="//Columns">
            <Column>
              <xsl:attribute name="ss:Width">
                <xsl:value-of select="Размер * 0.80"/>
              </xsl:attribute>
            </Column>
          </xsl:for-each>
          <Row>
            <Cell ss:StyleID="s89">
              <xsl:attribute name="ss:MergeAcross">
                <xsl:value-of select="count(//Columns) - 1" />
              </xsl:attribute>
              <Data ss:Type="String">
                <xsl:variable name="Дата" select="//Параметры[Имя='@Дата']/Значение" />
                <xsl:value-of select="concat(substring($Дата,9,2), '.', substring($Дата,6,2), '.', substring($Дата,1,4), ' ', substring($Дата,12,2), ':', substring($Дата,15,2), ':', substring($Дата,18,2))"/>
              </Data>
            </Cell>
          </Row>
          <Row>
            <Cell ss:StyleID="s90">
              <xsl:attribute name="ss:MergeAcross">
                <xsl:value-of select="count(//Columns) - 1" />
              </xsl:attribute>
              <Data ss:Type="String" xml:space="preserve"><xsl:value-of select="//Параметры[Имя='@Пользователь']/Значение"/></Data>
            </Cell>
          </Row>
          <Row ss:Height="21">
            <Cell ss:StyleID="s81">
              <xsl:attribute name="ss:MergeAcross">
                <xsl:value-of select="count(//Columns) - 1" />
              </xsl:attribute>
              <Data ss:Type="String">
                <xsl:value-of select="//Параметры[Имя='@НазваниеОтчета']/Значение"/>
              </Data>
            </Cell>
          </Row>
          <Row ss:AutoFitHeight="0" ss:Height="7.5">
            <Cell ss:StyleID="s84">
              <xsl:attribute name="ss:MergeAcross">
                <xsl:value-of select="count(//Columns) - 1" />
              </xsl:attribute>
            </Cell>
          </Row>
          <Row>
            <Cell>
              <Data ss:Type="String" xml:space="preserve">Организация: <xsl:value-of select="//Константа[НазваниеОбъекта = 'ОрганизацияПолноеНазвание']/ЗначениеКонстанты" /></Data>
            </Cell>
          </Row>
          <Row />
          <Row />
          <r:Data />
          <Row />
          <Row />
          <Row>
            <Cell>
              <Data ss:Type="String">Ответственный: _____________</Data>
            </Cell>
            <!--<Cell ss:Index="3" ss:StyleID="s102"/>-->
          </Row>
          <!--<Row ss:AutoFitHeight="0" ss:Height="18">
          <Cell ss:Index="3" ss:StyleID="s106"><Data ss:Type="String">подпись</Data></Cell>
         </Row>
         <Row>
          <Cell ss:Index="3"><Data ss:Type="String">М.П.</Data></Cell>
         </Row>-->
        </Table>
        <WorksheetOptions xmlns="urn:schemas-microsoft-com:office:excel">
          <PageSetup>
            <Layout x:Orientation="Landscape"/>
            <Header x:Margin="0.3"/>
            <Footer x:Margin="0.3"/>
            <PageMargins x:Bottom="0.75" x:Left="0.7" x:Right="0.7" x:Top="0.75"/>
          </PageSetup>
          <Print>
            <ValidPrinterInfo/>
            <PaperSizeIndex>9</PaperSizeIndex>
            <Scale>75</Scale>
            <HorizontalResolution>600</HorizontalResolution>
            <VerticalResolution>600</VerticalResolution>
          </Print>
          <PageBreakZoom>60</PageBreakZoom>
          <Selected/>
          <Panes />
          <ProtectObjects>False</ProtectObjects>
          <ProtectScenarios>False</ProtectScenarios>
        </WorksheetOptions>
      </Worksheet>
    </Workbook>

  </xsl:template>
</xsl:stylesheet>