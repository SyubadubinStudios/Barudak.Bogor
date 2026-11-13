[out:json][timeout:120][bbox:-6.782,106.624,-6.420,106.986];
(
way["railway"="rail"];
way["waterway"="river"];
way["highway"~"^(motorway|motorway_link|trunk|primary|trunk_link|primary_link)$"];
way["highway"="secondary"];
nwr["natural"="water"]["name"~"Situ Gede|Telaga Warna|Situ Burung|Cikaret",i];
node["railway"~"station|halt"];
);
out geom tags;
