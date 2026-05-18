import { initOldPcHub, renderOldPcHub } from './oldPcHub';
import { OldPcSection } from '../data/oldPcContent';

export const renderBrowser = (initialSection: OldPcSection = 'archive_search') => renderOldPcHub(initialSection);

export const initBrowser = (root: HTMLElement | Document = document, initialSection?: OldPcSection) => {
    initOldPcHub(root, initialSection);
};
