# Contributing to RPVoiceChat

Thanks for your interest in the mod! We welcome all contributions, but please read this document before submitting a pull request.

- [Questions and Problems](#question)
- [Issues and Bugs](#issue)
- [Before contributing](#contribute)
- [Working with the code](#working-with-the-code)

## <a name="question"></a> Where do I ask questions or report problems?

If you have a question about the mod, please feel free to shoot us a discord message (#ridderrasmus). If you have a problem with the mod, please [open an issue](#issue) on GitHub.

## <a name="issue"></a> I found a bug! What do I do?

If you find a bug in the mod, please [open an issue](https://github.com/Ridderrasmus/RPVoiceChat/issues/new?assignees=&labels=bug&projects=&template=bug_report.md&title=%5BBUG%5D+Bug+report+title) as it helps us keep track of what needs to be fixed.
Please include as much information as possible, including:
- The version of the mod you are using
- The version of Vintage Story you are using
- If you are using any other mods
- The steps to reproduce the bug (if possible)
- Game logs ideally (Game logs are located in the folder called `Logs` in the same direcory as the `Mods` folder)
	- Debug logs and crash logs are most useful
- Expected behaviour
- Screenshots if applicable
- Any other information you think might be relevant

Even better you could submit a [pull request](#contribute) with a fix for the bug!

## <a name="contribute"></a> I want to contribute! How do I do that?

We welcome contributions to the mod, but to avoid wasted work make sure to submit an issue as a potential enhancement before starting work on it.
This way we can discuss the feature and make sure it fits with the mod before you begin work on it and potentially waste time working on something that won't be accepted into the repo.
Once you have an issue open and we have discussed the feature and accepted it, you can begin work on it.
When you are done, submit a pull request following the process of the next section and we will review it and merge it into the mod if it looks good.

## <a name="working-with-the-code"></a> Working with the code

If you want to fix, add enhancement, or to improve the mod in other ways, you need to be able to work with GitHub, our code base, and also the rules regarding contributions to the repo.

### Setting up Environment

Just like with other mods using official mod template, you will need to set `VINTAGE_STORY` environment variable following [this guide](https://wiki.vintagestory.at/index.php/Modding:Setting_up_your_Development_Environment#Setup_the_Environment).

### Submitting your changes

After you finish making changes, create a Pull Request from your branch to `development`.
Make sure that your PR has no conflicts by merging/rebasing your branch on the `development` branch in the original repo.