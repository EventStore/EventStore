<?xml version="1.0" encoding="UTF-8" ?>
<xsl:stylesheet version="1.0"	
                exclude-result-prefixes="msxsl"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
		xmlns:msxsl="urn:schemas-microsoft-com:xslt">
<xsl:output method="html" indent="yes" />
<xsl:template match="test-results">
			<xsl:variable name="failedtests.list" select="//test-case[@success='False']"/>
			<xsl:variable name="total" select="@total + @not-run" />
   			<xsl:variable name="success" select="@total - @failures - @errors" />
          	Total  : <xsl:value-of select="$total" /> Success: <xsl:value-of select="$success" />Failed : <xsl:value-of select="@failures" /> Not Run: <xsl:value-of select="@not-run" />
			<xsl:apply-templates select="$failedtests.list"/>
</xsl:template>
<xsl:template match="test-case[@success='False']">
*************************************************************************************************	
	<xsl:value-of select="@name"/>
	<xsl:value-of select="failure/message"/>
	<xsl:value-of select="failure/stack-trace" />
</xsl:template>
</xsl:stylesheet>