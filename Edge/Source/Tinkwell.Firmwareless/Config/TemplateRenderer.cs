﻿using Fluid;
using System.Globalization;
using Tinkwell.Firmwareless.Config;

namespace Tinkwell.Bootstrapper.Expressions;

sealed class TemplateRenderer
{
    public static string Render(string content, object? parameters)
    {
        try
        {
            var context = new TemplateContext();
            ImportParameters(parameters, context);

            var parser = new FluidParser();
            return parser.Parse(content).Render(context);
        }
        catch (ParseException e)
        {
            throw new ConfigurationException($"Error rendering a template: {e.Message}", e);
        }
    }

    private static void ImportParameters(object? parameters, TemplateContext context)
    {
        if (parameters is not null)
        {
            if (parameters is System.Collections.IDictionary dictionary)
                ImportParametersFromDictionary(dictionary, context);
            else
                ImportParametersFromObject(parameters, context);
        }
    }

    private static void ImportParametersFromDictionary(System.Collections.IDictionary parameters, TemplateContext context)
    {
        foreach (System.Collections.DictionaryEntry kvp in parameters)
            context.SetValue(Convert.ToString(kvp.Key, CultureInfo.InvariantCulture)!, kvp.Value);
    }

    private static void ImportParametersFromObject(object parameters, TemplateContext context)
    {
        foreach (var property in parameters.GetType().GetProperties())
            context.SetValue(property.Name, property.GetValue(parameters));
    }
}