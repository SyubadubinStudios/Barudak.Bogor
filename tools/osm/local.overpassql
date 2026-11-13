[out:json][timeout:120];
(
way["highway"~"^(tertiary|tertiary_link|unclassified|residential|living_street|raceway)$"](-6.578,106.705,-6.538,106.762);
way["highway"="service"]["service"!~"parking_aisle|driveway|alley"](-6.568,106.715,-6.548,106.74);
way["highway"~"^(tertiary|tertiary_link|unclassified|residential|living_street|raceway)$"](-6.560,106.835,-6.510,106.885);
way["highway"~"^(tertiary|tertiary_link|unclassified|residential|living_street)$"](-6.730,106.925,-6.665,106.986);
);
out geom tags;
