using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace generatexml
{
    public class XmlGenerator
    {
        // Indent constants: 4 spaces per level
        private const string I1 = "    ";           // 1 level (4 spaces)
        private const string I2 = "        ";       // 2 levels (8 spaces)
        private const string I3 = "            ";   // 3 levels (12 spaces)

        public List<string> logstring = new List<string>();

        public void WriteXML(string xmlfilename, List<Question> QuestionList, string xmlPath)
        {
            try
            {
                if (xmlfilename.Substring(xmlfilename.Length - 3) == "_dd")
                {
                    xmlfilename = xmlfilename.Substring(0, xmlfilename.Length - 3);
                }
                else
                {
                    xmlfilename = xmlfilename.Substring(0, xmlfilename.Length - 4);
                }

                // These are strings for the first two of lines in the xml file
                string[] xmlStart = { "<?xml version='1.0' encoding='utf-8'?>", "<survey>" };

                // Open a XML file and start writing lines of text to it
                using (StreamWriter outputFile = new StreamWriter(string.Concat(xmlPath, xmlfilename, ".xml")))
                {
                    // Write the first 2 lines to the XML file
                    foreach (string line in xmlStart)
                        outputFile.WriteLine(line);

                    // Write a blank line
                    outputFile.WriteLine("");


                    // Iterate through each question object in the QuestionList list
                    // and write the necessary text to the XML file
                    foreach (Question question in QuestionList)
                    {
                        // Write the main part of the question
                        // Uses questionType, fieldName and fieldType
                                                outputFile.WriteLine(string.Concat(I1, "<question type='", question.questionType,
                                                                                   "' fieldname='", question.fieldName,
                                                                                   "' fieldtype='", question.fieldType, "'>"));


                        // Write the text if it is not a calculated question
                        if (question.questionType != "calculated")
                            outputFile.WriteLine(string.Concat(I2, "<text>", question.questionText.Replace("'", "&apos;"), "</text>"));

                        // Generate calculation XML for calculated questions with custom calculations
                        if (question.questionType == "calculated" && question.CalculationType != CalculationType.None)
                        {
                            GenerateCalculationXml(outputFile, question);
                        }

                        // The maximum characters if necessary
                        if (question.maxCharacters != "-9")
                            outputFile.WriteLine(string.Concat(I2, "<maxCharacters>", question.maxCharacters, "</maxCharacters>"));

                        // Input Mask
                        if (!string.IsNullOrEmpty(question.mask))
                            outputFile.WriteLine(string.Concat(I2, "<mask value=\"", question.mask, "\"/>"));


                        if (!string.IsNullOrEmpty(question.uniqueCheckMessage))
                        {
                            outputFile.WriteLine(I2 + "<unique_check>");
                            outputFile.WriteLine(string.Concat(I3, "<message>", question.uniqueCheckMessage, "</message>"));
                            outputFile.WriteLine(I2 + "</unique_check>");
                        }


                        // Upper and Lower range (numeric check)
                        if (question.questionType != "date" && question.lowerRange != "-9")
                        {
                            outputFile.WriteLine(I2 + "<numeric_check>");
                            outputFile.WriteLine(string.Concat(I3, "<values minvalue='", question.lowerRange, "' maxvalue='", question.upperRange, "' other_values='", question.lowerRange, "' message='Number must be between ", question.lowerRange, " and ", question.upperRange, "!'/>"));
                            outputFile.WriteLine(I2 + "</numeric_check>");
                        }

                        // Date range
                        if (question.questionType == "date")
                        {
                            outputFile.WriteLine(I2 + "<date_range>");
                            outputFile.WriteLine(string.Concat(I3, "<min_date>", question.lowerRange, "</min_date>"));
                            outputFile.WriteLine(string.Concat(I3, "<max_date>", question.upperRange, "</max_date>"));
                            outputFile.WriteLine(I2 + "</date_range>");
                        }

                        // Write responses if it is a radio or checkbox type question
                        if (question.questionType == "radio" || question.questionType == "checkbox" || question.questionType == "combobox")
                        {
                            outputFile.Write(I2 + "<responses");

                            if (question.ResponseSourceType == ResponseSourceType.Csv)
                            {
                                outputFile.Write($" source='csv' file='{question.ResponseSourceFile}'");
                            }
                            else if (question.ResponseSourceType == ResponseSourceType.Database)
                            {
                                outputFile.Write($" source='database' table='{question.ResponseSourceTable}'");
                            }
                            outputFile.WriteLine(">");

                            // Filters
                            foreach (var filter in question.ResponseFilters)
                            {
                                outputFile.WriteLine($"{I3}<filter column='{filter.Column}' operator='{filter.Operator}' value='{filter.Value}'/>");
                            }

                            // Display and Value
                            if (!string.IsNullOrEmpty(question.ResponseDisplayColumn))
                            {
                                outputFile.WriteLine($"{I3}<display column='{question.ResponseDisplayColumn}'/>");
                            }
                            if (!string.IsNullOrEmpty(question.ResponseValueColumn))
                            {
                                outputFile.WriteLine($"{I3}<value column='{question.ResponseValueColumn}'/>");
                            }

                            // Distinct
                            if (question.ResponseDistinct.HasValue)
                            {
                                outputFile.WriteLine($"{I3}<distinct>{question.ResponseDistinct.Value.ToString().ToLower()}</distinct>");
                            }

                            // Empty Message
                            if (!string.IsNullOrEmpty(question.ResponseEmptyMessage))
                            {
                                outputFile.WriteLine($"{I3}<empty_message>{question.ResponseEmptyMessage}</empty_message>");
                            }

                            // Don't Know
                            if (!string.IsNullOrEmpty(question.ResponseDontKnowValue))
                            {
                                string labelAttr = string.IsNullOrEmpty(question.ResponseDontKnowLabel) ? "" : $" label='{question.ResponseDontKnowLabel}'";
                                outputFile.WriteLine($"{I3}<dont_know value='{question.ResponseDontKnowValue}'{labelAttr}/>");
                            }

                            // Not In List
                            if (!string.IsNullOrEmpty(question.ResponseNotInListValue))
                            {
                                string labelAttr = string.IsNullOrEmpty(question.ResponseNotInListLabel) ? "" : $" label='{question.ResponseNotInListLabel}'";
                                outputFile.WriteLine($"{I3}<not_in_list value='{question.ResponseNotInListValue}'{labelAttr}/>");
                            }


                            if (question.ResponseSourceType == ResponseSourceType.Static)
                            {
                                string[] responses = question.responses.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                                if (responses.Length == 0)
                                {
                                    outputFile.WriteLine(I3 + "<response></response>");
                                }
                                else
                                {
                                    foreach (string response in responses)
                                    {
                                        int index = response.IndexOf(@":");
                                        outputFile.WriteLine(string.Concat(I3, "<response value='", response.Substring(0, index), "'>",
                                                                            response.Substring(index + 1).Trim(), "</response>"));
                                    }
                                }
                            }

                            outputFile.WriteLine(I2 + "</responses>");
                        }

                        //  Logic Checks
                        foreach (string logicCheck in question.logicChecks)
                        {
                            // New format: just output the logic check directly
                            outputFile.WriteLine(I2 + "<logic_check>");
                            outputFile.WriteLine(GenerateLogicChecks(logicCheck));
                            outputFile.WriteLine(I2 + "</logic_check>");
                        }


                        // Skips
                        if (question.skip != "")
                        {
                            // This stores the text for the skip
                            string[] skips = question.skip.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                            // Lists to store preskips and postskips
                            List<string> preSkips = new List<string>();
                            List<string> postSkips = new List<string>();


                            // Populate the list for each type of skip
                            foreach (string skip in skips)
                            {
                                int index = skip.IndexOf(@":");

                                if (skip.Substring(0, index) == "preskip")
                                    preSkips.Add(skip);

                                if (skip.Substring(0, index) == "postskip")
                                    postSkips.Add(skip);
                            }


                            // Create text preskips
                            if (preSkips.Count > 0)
                            {
                                outputFile.WriteLine(I2 + "<preskip>");
                                foreach (string preSkip in preSkips)
                                {
                                    // Call the GenerateSkips() function
                                    outputFile.WriteLine(GenerateSkips(preSkip, "preSkip"));
                                }
                                outputFile.WriteLine(I2 + "</preskip>");
                            }


                            // Create text postskips
                            if (postSkips.Count > 0)
                            {
                                outputFile.WriteLine(I2 + "<postskip>");
                                // Call the GenerateSkips() function
                                foreach (string postSkip in postSkips)
                                {
                                    outputFile.WriteLine(GenerateSkips(postSkip, "postSkip"));
                                }
                                outputFile.WriteLine(I2 + "</postskip>");
                            }
                        }



                        // Don't know
                        if (question.dontKnow == "TRUE" || question.dontKnow == "True")
                            outputFile.WriteLine(I2 + "<dont_know>-7</dont_know>");

                        // Refuse to answer
                        if (question.refuse == "TRUE" || question.refuse == "True")
                            outputFile.WriteLine(I2 + "<refuse>-8</refuse>");

                        // Not applicable
                        if (question.na == "TRUE" || question.na == "True")
                            outputFile.WriteLine(I2 + "<na>-6</na>");

                        // Close off the question
                        outputFile.WriteLine(I1 + "</question>");
                        outputFile.WriteLine("\n");
                    }

                    // The last 'info' question ending every survey
                    string[] xmlEnd = {I1 + "<question type='information' fieldname='end_of_questions' fieldtype='n/a'>",
                                   I2 + "<text>Press the &apos;Finish&apos; button to save the data.</text>", I1 + "</question>" };
                    foreach (string line in xmlEnd)
                        outputFile.WriteLine(line);

                    outputFile.WriteLine("");
                    outputFile.WriteLine("</survey>");
                }
            }


            // Error handling in caase we could not create the XML file
            catch (Exception ex)
            {
                MessageBox.Show("ERROR - Writing to XML file: Could not create XML file " + xmlfilename + " Ensure path is correct." + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logstring.Add("ERROR - Writing to XML file: Could not create XML file " + xmlfilename + " Ensure path is correct." + ex.Message);
            }
        }



        //////////////////////////////////////////////////////////////////////
        // Function to generate the text for the skips
        //////////////////////////////////////////////////////////////////////
        private string GenerateSkips(string skip, string skipType)
        {
            // Number of initial characters depending on whether it's a preskip or postskip
            int lenSkip = skipType == "postSkip" ? 13 : 12;


            // Create a list to store the index of each 'space' in the skip text
            var spaceIndices = new List<int>();

            // Populate the spaceIndices list
            for (int i = 0; i < skip.Length; i++)
                if (skip[i] == ' ') spaceIndices.Add(i);


            // Get the name of the field to check for skip
            string fieldname_to_check = skip.Substring(lenSkip, spaceIndices[2] - spaceIndices[1] - 1);

            // Variables to store the condition and the value of the skip
            string condition;
            string value;

            // If there are 9 spaces, then we know that the condition is 'does not contain'
            if (spaceIndices.Count == 9)
            {
                // Get the condition
                condition = "does not contain";
                // Get the value
                value = skip.Substring(spaceIndices[5] + 1, spaceIndices[6] - spaceIndices[5] - 2);
            }
            // Check if the skip has 'contains'
            else if (skip.Contains("contains"))
            {
                // Get the condition
                condition = "contains";
                // Get the value
                value = skip.Substring(spaceIndices[3] + 1, spaceIndices[4] - spaceIndices[3] - 2);
            }
            // Skip does not have 'does not contain' or 'contains'
            else
            {
                // Get the condition
                condition = skip.Substring(spaceIndices[2] + 1, spaceIndices[3] - spaceIndices[2] - 1);

                // Replace '<' and '>' symbols, if necessary
                condition = condition.Replace("<", "&lt;");
                condition = condition.Replace(">", "&gt;");

                // Get the value
                value = skip.Substring(spaceIndices[3] + 1, spaceIndices[4] - spaceIndices[3] - 2);
            }

            // Get the field name to skip to
            string fieldname_to_skip_to = skip.Substring(spaceIndices[spaceIndices.Count - 1] + 1);

            // Determine response_type based on value format
            string responseType;
            string responseValue;

            if (double.TryParse(value, out _))
            {
                // Numeric value = fixed
                responseType = "fixed";
                responseValue = value;
            }
            else if (value.StartsWith("[[") && value.EndsWith("]]"))
            {
                // Field reference in double brackets = dynamic
                responseType = "dynamic";
                responseValue = value.Substring(2, value.Length - 4); // Remove [[ and ]]
            }
            else
            {
                // This shouldn't happen if validation passed, but handle gracefully
                responseType = "fixed";
                responseValue = value;
            }

            // Build the string and return it
            return string.Concat(I3, "<skip fieldname='", fieldname_to_check,
                                 "' condition='", condition,
                                 "' response='", responseValue,
                                 "' response_type='", responseType,
                                 "' skiptofieldname='",
                                 fieldname_to_skip_to, "'/>");
        }



        //////////////////////////////////////////////////////////////////////
        // Function to generate the text for the logic checks
        //////////////////////////////////////////////////////////////////////
        private string GenerateLogicChecks(string logicCheck)
        {
            // New format: expression; 'error message'
            // Example: tabletnum2 != tabletnum; 'This does not match your previous entry!'

            // Split by semicolon to get expression and message
            string[] parts = logicCheck.Split(new char[] { ';' }, 2);
            string expression = parts[0].Trim();
            string message = parts[1].Trim();

            // Replace operators with XML entities
            expression = expression.Replace("!=", "&lt;&gt;");
            expression = expression.Replace("<>", "&lt;&gt;");
            expression = expression.Replace("<=", "&lt;=");
            expression = expression.Replace(">=", "&gt;=");
            // Replace individual < and > that aren't part of <= or >=
            expression = Regex.Replace(expression, @"(?<!&lt;)(?<!&gt;)<(?!=)", "&lt;");
            expression = Regex.Replace(expression, @"(?<!&lt;=)(?<!&gt;=)>(?!=)", "&gt;");

            StringBuilder result = new StringBuilder();

            // Check if expression contains 'or' - if so, format it across multiple lines
            if (expression.Contains(" or "))
            {
                string[] orParts = expression.Split(new string[] { " or " }, StringSplitOptions.None);

                for (int i = 0; i < orParts.Length; i++)
                {
                    result.Append(I3);
                    result.Append(orParts[i].Trim());

                    if (i < orParts.Length - 1)
                    {
                        result.Append(" or");
                        result.AppendLine();
                    }
                }
                result.AppendLine(";");
                result.Append(I3);
                result.Append(message);
            }
            else
            {
                // Single line format
                result.Append(I3);
                result.Append(expression);
                result.Append("; ");
                result.Append(message);
            }

            return result.ToString();
        }


        //////////////////////////////////////////////////////////////////////
        // Function to generate the XML for calculated field calculations
        //////////////////////////////////////////////////////////////////////
        private void GenerateCalculationXml(StreamWriter outputFile, Question question)
        {
            switch (question.CalculationType)
            {
                case CalculationType.Query:
                    GenerateQueryCalculation(outputFile, question);
                    break;

                case CalculationType.Case:
                    GenerateCaseCalculation(outputFile, question);
                    break;

                case CalculationType.Constant:
                    outputFile.WriteLine($"{I2}<calculation type='constant' value='{question.CalculationConstantValue}'/>");
                    break;

                case CalculationType.Lookup:
                    outputFile.WriteLine($"{I2}<calculation type='lookup' field='{question.CalculationLookupField}'/>");
                    break;

                case CalculationType.Math:
                    GenerateMathCalculation(outputFile, question);
                    break;

                case CalculationType.Concat:
                    GenerateConcatCalculation(outputFile, question);
                    break;

                case CalculationType.AgeFromDate:
                    outputFile.WriteLine($"{I2}<calculation type='age_from_date' field='{question.CalculationLookupField}' value='{question.CalculationConstantValue}'/>");
                    break;

                case CalculationType.AgeAtDate:
                    string separatorAttr = string.IsNullOrEmpty(question.CalculationConcatSeparator)
                        ? ""
                        : $" separator='{question.CalculationConcatSeparator}'";
                    outputFile.WriteLine($"{I2}<calculation type='age_at_date' field='{question.CalculationLookupField}' value='{question.CalculationConstantValue}'{separatorAttr}/>");
                    break;

                case CalculationType.DateOffset:
                    outputFile.WriteLine($"{I2}<calculation type='date_offset' field='{question.CalculationLookupField}' value='{question.CalculationConstantValue}'/>");
                    break;

                case CalculationType.DateDiff:
                    outputFile.WriteLine($"{I2}<calculation type='date_diff' field='{question.CalculationLookupField}' value='{question.CalculationConstantValue}' unit='{question.CalculationUnit}'/>");
                    break;
            }
        }

        private void GenerateQueryCalculation(StreamWriter outputFile, Question question)
        {
            outputFile.WriteLine(I2 + "<calculation type='query'>");
            outputFile.WriteLine($"{I3}<sql>{question.CalculationQuerySql}</sql>");

            foreach (var param in question.CalculationQueryParameters)
            {
                // Keep the @ prefix in the parameter name
                outputFile.WriteLine($"{I3}<parameter name='{param.Name}' field='{param.FieldName}'/>");
            }

            outputFile.WriteLine(I2 + "</calculation>");
        }

        private void GenerateCaseCalculation(StreamWriter outputFile, Question question)
        {
            outputFile.WriteLine(I2 + "<calculation type='case'>");

            foreach (var condition in question.CalculationCaseConditions)
            {
                // Convert operators to XML entities
                string xmlOperator = ConvertOperatorToXml(condition.Operator);

                outputFile.WriteLine($"{I3}<when field='{condition.Field}' operator='{xmlOperator}' value='{condition.Value}'>");

                // Generate result (typically a constant)
                if (condition.Result != null)
                {
                    GenerateCalculationPart(outputFile, condition.Result, 4);
                }

                outputFile.WriteLine(I3 + "</when>");
            }

            // Generate else clause if present
            if (question.CalculationCaseElse != null)
            {
                outputFile.WriteLine(I3 + "<else>");
                GenerateCalculationPart(outputFile, question.CalculationCaseElse, 4);
                outputFile.WriteLine(I3 + "</else>");
            }

            outputFile.WriteLine(I2 + "</calculation>");
        }

        private void GenerateMathCalculation(StreamWriter outputFile, Question question)
        {
            outputFile.WriteLine($"{I2}<calculation type='math' operator='{question.CalculationMathOperator}'>");

            foreach (var part in question.CalculationMathParts)
            {
                GenerateCalculationPart(outputFile, part, 3);
            }

            outputFile.WriteLine(I2 + "</calculation>");
        }

        private void GenerateConcatCalculation(StreamWriter outputFile, Question question)
        {
            string separatorAttr = string.IsNullOrEmpty(question.CalculationConcatSeparator)
                ? ""
                : $" separator='{question.CalculationConcatSeparator}'";

            outputFile.WriteLine($"{I2}<calculation type='concat'{separatorAttr}>");

            foreach (var part in question.CalculationConcatParts)
            {
                GenerateCalculationPart(outputFile, part, 3);
            }

            outputFile.WriteLine(I2 + "</calculation>");
        }

        private void GenerateCalculationPart(StreamWriter outputFile, CalculationPart part, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);

            switch (part.Type)
            {
                case CalculationType.Constant:
                    outputFile.WriteLine($"{indent}<result type='constant' value='{part.ConstantValue}'/>");
                    break;

                case CalculationType.Lookup:
                    outputFile.WriteLine($"{indent}<part type='lookup' field='{part.LookupField}'/>");
                    break;

                case CalculationType.Query:
                    outputFile.WriteLine($"{indent}<part type='query'>");
                    outputFile.WriteLine($"{indent}    <sql>{part.QuerySql}</sql>");
                    foreach (var param in part.QueryParameters)
                    {
                        // Keep the @ prefix in the parameter name
                        outputFile.WriteLine($"{indent}    <parameter name='{param.Name}' field='{param.FieldName}'/>");
                    }
                    outputFile.WriteLine($"{indent}</part>");
                    break;

                case CalculationType.Math:
                    outputFile.WriteLine($"{indent}<part type='math' operator='{part.MathOperator}'>");
                    foreach (var nestedPart in part.Parts)
                    {
                        GenerateCalculationPart(outputFile, nestedPart, indentLevel + 1);
                    }
                    outputFile.WriteLine($"{indent}</part>");
                    break;

                case CalculationType.Concat:
                    string separatorAttr = string.IsNullOrEmpty(part.ConcatSeparator)
                        ? ""
                        : $" separator='{part.ConcatSeparator}'";
                    outputFile.WriteLine($"{indent}<part type='concat'{separatorAttr}>");
                    foreach (var nestedPart in part.Parts)
                    {
                        GenerateCalculationPart(outputFile, nestedPart, indentLevel + 1);
                    }
                    outputFile.WriteLine($"{indent}</part>");
                    break;
            }
        }

        private string ConvertOperatorToXml(string op)
        {
            switch (op.Trim())
            {
                case "=": return "=";
                case "!=": return "!=";
                case "<>": return "&lt;&gt;";
                case ">": return "&gt;";
                case "<": return "&lt;";
                case ">=": return "&gt;=";
                case "<=": return "&lt;=";
                default: return "=";
            }
        }
    }
}