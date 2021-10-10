<p align="center">
 <b>Made by Boppy Games :)</b><br>
    <a href="https://discord.com/invite/yY9wHNn">
        <img src="https://img.shields.io/discord/731217831898906737?logo=discord"
            alt="chat on Discord"></a>
    <a href="https://twitter.com/intent/follow?screen_name=boppygames">
        <img src="https://img.shields.io/twitter/follow/boppygames?style=social&logo=twitter"
            alt="follow on Twitter"></a>
    <a href="https://www.twitch.tv/boppygames">
        <img alt="Twitch Status" src="https://img.shields.io/twitch/status/boppygames?style=social"></a>
    <br><a href="https://store.steampowered.com/app/1384030/Boppio/">Checkout Boppio on Steam!</a>
</p>

Checkout Boppio on Steam! https://store.steampowered.com/app/1384030/Boppio/

# Unity Save System

Hello everyone! This is the save system that I wrote on stream. The objective of this save system is ease-of-use. You should only have to apply a Save attribute to fields within components in order to save data. This system can serialize most types out of the box, but also supports custom serialization.

This should work with basically any version of Unity, but the specific version I used on stream was 2020.3.19f1 LTS.

## Getting Started

 - Go to the releases page and download the latest Unity package.
 - Import the package into your project, it should be within the SaveSystem directory
 - Open up the demo scene and give it a try.

Again, the main drawback of this system is that each asset needs to be registered in the `AssetSaveList` in order to be saved. If you forget to put an asset into the `AssetSaveList` before saving, it will not be saved or loaded (you will get an error).
