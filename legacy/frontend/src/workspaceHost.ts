/** Only presentation-side file delivery varies by host. Decisions stay on the API. */
export interface WorkspaceHost {
 saveJson: (filename: string, originalJson: string) => void;
}

export const browserWorkspaceHost: WorkspaceHost = {
 saveJson(filename, originalJson) {
  const url=URL.createObjectURL(new Blob([originalJson],{type:'application/json'}));
  const link=document.createElement('a');link.href=url;link.download=filename;
  link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
 }
};
