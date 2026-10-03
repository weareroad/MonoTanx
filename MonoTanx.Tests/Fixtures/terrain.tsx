<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" name="terrain" tilewidth="16" tileheight="16" tilecount="8" columns="8">
 <image source="terrain.png" width="128" height="16"/>
 <tile id="0"><properties><property name="TerrainKind" value="Ground"/></properties></tile>
 <tile id="1"><properties><property name="TerrainKind" value="Water"/></properties></tile>
 <tile id="2"><properties><property name="TerrainKind" value="Bridge"/></properties></tile>
 <tile id="3"><properties><property name="TerrainKind" value="Wall"/></properties></tile>
 <tile id="4"><properties><property name="TerrainKind" value="Ravine"/></properties></tile>
 <tile id="5"><properties><property name="TerrainKind" value="Hill"/></properties></tile>
 <tile id="6"><properties><property name="TerrainKind" value="Reflective"/></properties></tile>
 <tile id="7"><properties>
  <property name="TerrainKind" value="Ground"/>
  <property name="MovementSpeedMultiplier" type="float" value="0.5"/>
  <property name="FuelCostMultiplier" type="float" value="2"/>
 </properties></tile>
</tileset>
