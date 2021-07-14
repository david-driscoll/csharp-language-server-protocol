This is a samson docs theme adapted from ?.

# Installation

Statiq themes go in a `theme` folder alongside your `input` folder. If your site is inside a git repository, you can add the theme as a git submodule:

```
git submodule add https://github.com/statiqdev/Samson.git theme
```

Alternatively you can clone the theme directly:

```
git clone https://github.com/statiqdev/Samson.git theme
```

Once inside the `theme` folder, Statiq will automatically recognize the theme. If you want to tweak the theme you can edit files directly in the `theme` folder or copy them to your `input` folder and edit them there.

# Settings

## Global

**TODO**

## Page

**TODO**

## Calculated

The following keys are calculated in `settings.yml` and can be overridden by providing new values in your settings or front matter or used from your own pages.

**TODO**

# Partials

Replace or copy any of these Razor partials in your `input` folder to override sections of the site:

**TODO**

# Sections

In addition to globally changing sections of the site using the partials above you can also add the following Razor sections to any given page to override them for that page (which will typically disable the use of the corresponding partial):

**TODO**

# Index Page

You can provide your own `index.cshtml` (or `index.md`) if you like and that will override the default index page. You'll _have_ to provide your own index page if you don't
include any blog posts since the default index page is an archive of posts.

# Styles

To add new styles or override existing ones, create an input file at `scss/_overrides.scss` and add Sass styles there.

# Porting From Wyam

This Samson theme is roughly compatible with the Wyam Samson theme. Some notes if you're porting:

- You will need to [create a Statiq Docs app](https://statiq.dev/docs/) at the root of your site (you can keep the `input` directory).
  - Run `dotnet new console` at the root of your site.
  - Run `dotnet add package Statiq.Docs --version x.y.z` (using the [latest Statiq Docs version](https://www.nuget.org/packages/Statiq.Docs)).
  - Change the generated `Program` class in `Program.cs` to:

```
using System;
using System.Threading.Tasks;
using Statiq.App;
using Statiq.Docs;

namespace ...
{
  public class Program
  {
    public static async Task<int> Main(string[] args) =>
      await Bootstrapper
        .Factory
        .CreateDocs(args)
        .RunAsync();
  }
}
```

- Follow the [installation instructions above](#installation) to install the theme into your site.

- Create a `settings.yml` file at the root of your site and copy over settings from your `config.wyam` file
  - Since the new settings file is YAML you don't need to prefix strings or anything, for example `Settings[Keys.Host] = "daveaglick.com";` becomes `Host: daveaglick.com`.
  - If you defined a global "Title" setting in `config.wyam` the new theme should set "SiteTitle" instead (and if not, a "SiteTitle" should be defined).
  - If you defined an "Intro" setting, that should be placed in a new `_index.yml` file in your `input` directory with a key of "Description".

- If you created an `input/assets/css/override.css` file, move it to `input/scss/_overrides.scss` (and you can now use Sass inside the CSS overrides file).

- Replace any uses of `img-response` CSS class with `img-fluid` since this theme uses a newer version of Bootstrap and that CSS class changed.

- Rename and fix up any override theme files or partials according to the supported ones documented above.
  - For example, the old Wyam CleanBlog supported a `_PostFooter.cshtml` which should be renamed to `_post-footer.cshtml`.
  - The CSS may not match exactly since the new CleanBlog theme is based on the most recent CleanBlog project so you may need to take a look at the default partial implementations in this theme and adjust your override files accordingly.

- You can likely remove any build scripting and bootstrapping code since you can now run `dotnet run -- preview` to preview the site.
  - You can also now setup [built-in deployment](https://statiq.dev/web/deployment/).
