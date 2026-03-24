import overviewData from '../../content/agentic/overview.json';
import pillarsData from '../../content/agentic/pillars.json';
import lifecycleData from '../../content/agentic/lifecycle.json';
import artifactsData from '../../content/agentic/artifacts.json';
import offeringsData from '../../content/agentic/offerings.json';
import rolesData from '../../content/agentic/roles.json';
import artifactsExpandedData from '../../content/agentic/artifacts-expanded.json';
import navigationData from '../../content/agentic/navigation.json';
import homepageData from '../../content/agentic/homepage.json';
import diagramsData from '../../content/agentic/diagrams.json';
import useCasesData from '../../content/agentic/use-cases.json';
import ctaData from '../../content/agentic/cta.json';
import siteMapData from '../../content/agentic/site-map.json';
import homepagePolishData from '../../content/agentic/homepage-polish.json';
import polishedDiagramsData from '../../content/agentic/polished-diagrams.json';
import sectionAnchorsData from '../../content/agentic/section-anchors.json';
import virtualArchitectData from '../../content/agentic/virtual-architect.json';

export function getAgenticOverview() {
  return overviewData?.default ?? overviewData;
}

export function getAgenticPillars() {
  const data = pillarsData?.default ?? pillarsData;
  return [...data].sort((a, b) => a.title.localeCompare(b.title));
}

export function getAgenticPillarBySlug(slug) {
  return getAgenticPillars().find((item) => item.slug === slug);
}

export function getAgenticLifecyclePhases() {
  const data = lifecycleData?.default ?? lifecycleData;
  return [...data];
}

export function getAgenticLifecycleBySlug(slug) {
  return getAgenticLifecyclePhases().find((item) => item.slug === slug);
}

export function getAgenticArtifacts() {
  const data = artifactsData?.default ?? artifactsData;
  return [...data];
}

export function getAgenticOfferings() {
  const data = offeringsData?.default ?? offeringsData;
  return [...data];
}

export function getAgenticRoles() {
  const data = rolesData?.default ?? rolesData;
  return data;
}

export function getAgenticArtifactsExpanded() {
  const data = artifactsExpandedData?.default ?? artifactsExpandedData;
  return [...data];
}

export function getAgenticNavigation() {
  const data = navigationData?.default ?? navigationData;
  return data;
}

export function getAgenticHomepage() {
  const data = homepageData?.default ?? homepageData;
  return data;
}

export function getAgenticDiagrams() {
  const data = diagramsData?.default ?? diagramsData;
  return data;
}

export function getAgenticUseCases() {
  const data = useCasesData?.default ?? useCasesData;
  return data;
}

export function getAgenticCta() {
  const data = ctaData?.default ?? ctaData;
  return data;
}

export function getAgenticSiteMap() {
  const data = siteMapData?.default ?? siteMapData;
  return data;
}

export function getAgenticHomepagePolish() {
  const data = homepagePolishData?.default ?? homepagePolishData;
  return data;
}

export function getAgenticPolishedDiagrams() {
  const data = polishedDiagramsData?.default ?? polishedDiagramsData;
  return data;
}

export function getAgenticSectionAnchors() {
  const data = sectionAnchorsData?.default ?? sectionAnchorsData;
  return data;
}

export function getAgenticVirtualArchitect() {
  const data = virtualArchitectData?.default ?? virtualArchitectData;
  return data;
}
